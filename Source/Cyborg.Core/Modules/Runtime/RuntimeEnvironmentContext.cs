using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using Cyborg.Core.Modules.Runtime.Transactions;
using Cyborg.Core.Modules.Runtime.Transactions.Core;
using Cyborg.Core.Text;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Runtime;

internal sealed class RuntimeEnvironmentContext
{
    private const string UNBOUND_ENVIRONMENT = "__UNBOUND";

    private readonly RuntimeEnvironmentTransactionParticipant _environments;
    private readonly ILogger _logger;
    private readonly RuntimeEnvironmentContext? _parent;
    private readonly VariableSyntaxBuilder _syntaxFactory;
    private readonly ExecutionTransaction _transaction;

    public IRuntimeEnvironment GlobalEnvironment { get; }

    public IRuntimeEnvironment ParentEnvironment => _parent?.Environment ?? GlobalEnvironment;

    public IRuntimeEnvironment Environment { get; }

    public VariableSyntaxBuilder SyntaxFactory => _syntaxFactory;

    private RuntimeEnvironmentContext(
        RuntimeEnvironmentTransactionParticipant environments,
        ExecutionTransaction transaction,
        VariableSyntaxBuilder syntaxFactory,
        ILogger logger,
        RuntimeEnvironmentContext? parent,
        IRuntimeEnvironment globalEnvironment,
        IRuntimeEnvironment environment)
    {
        _environments = environments;
        _transaction = transaction;
        _syntaxFactory = syntaxFactory;
        _logger = logger;
        _parent = parent;
        GlobalEnvironment = globalEnvironment;
        Environment = environment;
    }

    public static RuntimeEnvironmentContext CreateRoot(
        GlobalRuntimeEnvironment globalEnvironment,
        RuntimeEnvironmentTransactionParticipant environments,
        ExecutionTransaction transaction,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(globalEnvironment);
        ArgumentNullException.ThrowIfNull(environments);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ILogger logger = loggerFactory.CreateLogger("cyborg.core.runtime");
        RuntimeEnvironmentTransactionState state = transaction.GetParticipantState(environments);
        if (state.GlobalEnvironmentId != ((ITransactionalRuntimeEnvironment)globalEnvironment).EnvironmentId)
        {
            throw new InvalidOperationException("The runtime environment transaction seed does not match the supplied logical global environment.");
        }
        IRuntimeEnvironment transactionalGlobal = ((ITransactionalRuntimeEnvironment)globalEnvironment).BindTransaction(environments, transaction);
        return new RuntimeEnvironmentContext(
            environments,
            transaction,
            globalEnvironment.SyntaxFactory,
            logger,
            parent: null,
            transactionalGlobal,
            transactionalGlobal);
    }

    public RuntimeEnvironmentContext CreateTransactionView(ExecutionTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        RuntimeEnvironmentContext? parent = _parent?.CreateTransactionView(transaction);
        IRuntimeEnvironment globalEnvironment = BindEnvironment(GlobalEnvironment, transaction);
        IRuntimeEnvironment environment = BindEnvironment(Environment, transaction);
        return new RuntimeEnvironmentContext(
            _environments,
            transaction,
            _syntaxFactory,
            _logger,
            parent,
            globalEnvironment,
            environment);
    }

    public RuntimeEnvironmentContext CreateChild(IRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        IRuntimeEnvironment transactionalEnvironment = BindEnvironment(environment);
        return new RuntimeEnvironmentContext(
            _environments,
            _transaction,
            _syntaxFactory,
            _logger,
            this,
            GlobalEnvironment,
            transactionalEnvironment);
    }

    public IRuntimeEnvironment BindEnvironment(IRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return BindEnvironment(environment, _transaction);
    }

    public IRuntimeEnvironment PrepareEnvironment(ModuleEnvironment moduleEnvironment, IReadOnlyCollection<string>? overrideResolutionTags = null)
    {
        ArgumentNullException.ThrowIfNull(moduleEnvironment);
        IRuntimeEnvironment? environment = null;
        if (moduleEnvironment.Scope is EnvironmentScope.Reference)
        {
            if (string.IsNullOrEmpty(moduleEnvironment.Name))
            {
                throw new InvalidOperationException("Attempting to reference an environment without providing an environment name.");
            }
            if (!TryGetEnvironment(moduleEnvironment.Name, out environment))
            {
                throw new InvalidOperationException($"Attempting to reference an environment that does not exist: {moduleEnvironment.Name}");
            }
            _logger.LogNamedEnvironmentResolved(moduleEnvironment.Name);
        }
        environment ??= CreateEnvironment(moduleEnvironment.Scope, moduleEnvironment.Name, moduleEnvironment.Transient);
        if (overrideResolutionTags is not null)
        {
            foreach (string tag in overrideResolutionTags)
            {
                if (!SyntaxFactory.IsValidIdentifier(tag))
                {
                    throw new InvalidOperationException($"Override resolution tags must be valid identifiers: \"{tag}\"");
                }
            }
            _logger.LogOverrideTagsApplied(string.Join(", ", overrideResolutionTags), environment.Name);
            environment = environment.WithOverrideResolutionTags(overrideResolutionTags);
        }
        return environment;
    }

    public IRuntimeEnvironment? ResolveEnvironmentReference(ModuleEnvironmentReference environmentReference)
    {
        ArgumentNullException.ThrowIfNull(environmentReference);
        return environmentReference switch
        {
            { Scope: EnvironmentScopeReference.Current } => Environment,
            { Scope: EnvironmentScopeReference.Global } => GlobalEnvironment,
            { Scope: EnvironmentScopeReference.Parent } => ParentEnvironment,
            { Scope: EnvironmentScopeReference.Reference, Name: { } name } when TryGetEnvironment(name, out IRuntimeEnvironment? environment) => environment,
            _ => null
        };
    }

    private IRuntimeEnvironment BindEnvironment(IRuntimeEnvironment environment, ExecutionTransaction transaction)
    {
        if (environment is not ITransactionalRuntimeEnvironment transactionalEnvironment)
        {
            throw new InvalidOperationException($"Runtime environment type '{environment.GetType().FullName}' does not expose transactional environment identity.");
        }
        EnsureEnvironmentExists(environment, transaction);
        return transactionalEnvironment.BindTransaction(_environments, transaction);
    }

    private IRuntimeEnvironment CreateEnvironment(EnvironmentScope scope, string? name, bool transient)
    {
        if (string.IsNullOrEmpty(name))
        {
            transient = true;
            name = Guid.CreateVersion7().ToString();
        }
        ITaggedStringConversionObserver? conversionObserver = Environment is EnvironmentLike environmentLike
            ? environmentLike.TaggedStringConversionObserver
            : null;
        IRuntimeEnvironment environment;
        switch (scope)
        {
            case EnvironmentScope.Isolated:
                environment = CreateEnvironmentNode(name, transient, parent: null, conversionObserver);
                break;
            case EnvironmentScope.Global:
                environment = GlobalEnvironment;
                break;
            case EnvironmentScope.InheritParent:
                environment = CreateEnvironmentNode(name, transient, CreateParentReference(Environment), conversionObserver);
                break;
            case EnvironmentScope.InheritGlobal:
                environment = CreateEnvironmentNode(name, transient, CreateParentReference(GlobalEnvironment), conversionObserver);
                break;
            case EnvironmentScope.Parent:
                environment = ParentEnvironment.Bind(UNBOUND_ENVIRONMENT);
                break;
            case EnvironmentScope.Current:
                environment = Environment.Bind(UNBOUND_ENVIRONMENT);
                break;
            case EnvironmentScope.Reference:
                throw new ArgumentException("Attempting to create an environment by reference without providing an environment reference.", nameof(scope));
            default:
                throw new ArgumentOutOfRangeException(nameof(scope), scope, "Invalid environment scope.");
        }
        _logger.LogEnvironmentCreated(scope.ToString(), environment.Name);
        return environment;
    }

    private IRuntimeEnvironment CreateEnvironmentNode(
        string name,
        bool transient,
        RuntimeEnvironmentParent? parent,
        ITaggedStringConversionObserver? conversionObserver)
    {
        RuntimeEnvironmentId environmentId = RuntimeEnvironmentId.Create();
        RuntimeEnvironmentNode node = new(name, transient, parent, conversionObserver);
        RuntimeEnvironmentTransactionState state = GetState(_transaction);
        if (transient)
        {
            state.AddEnvironment(environmentId, node, values: []);
        }
        else if (!state.TryAddNamedEnvironment(environmentId, node, values: []))
        {
            throw new InvalidOperationException($"Attempting to create a named environment that already exists: {name}");
        }
        return CreateEnvironmentView(environmentId, UNBOUND_ENVIRONMENT, overrideResolutionTags: [], _transaction);
    }

    private IRuntimeEnvironment CreateEnvironmentView(
        RuntimeEnvironmentId environmentId,
        string ns,
        IReadOnlyCollection<string> overrideResolutionTags,
        ExecutionTransaction? transaction = null)
    {
        transaction ??= _transaction;
        RuntimeEnvironmentTransactionState state = GetState(transaction);
        IRuntimeEnvironment environment = CreateEnvironmentViewCore(
            environmentId,
            ns,
            transaction,
            state,
            visited: []);
        return overrideResolutionTags.Count == 0
            ? environment
            : environment.WithOverrideResolutionTags(overrideResolutionTags);
    }

    private IRuntimeEnvironment CreateEnvironmentViewCore(
        RuntimeEnvironmentId environmentId,
        string ns,
        ExecutionTransaction transaction,
        RuntimeEnvironmentTransactionState state,
        HashSet<RuntimeEnvironmentId> visited)
    {
        if (!visited.Add(environmentId))
        {
            throw new InvalidOperationException("Runtime environment topology contains an inheritance cycle.");
        }
        if (!state.TryGetEnvironment(environmentId, out RuntimeEnvironmentNode? node))
        {
            throw new InvalidOperationException($"Runtime environment '{environmentId}' does not exist in the current transaction.");
        }

        IRuntimeEnvironment environment;
        if (node.Parent is RuntimeEnvironmentParent parentReference)
        {
            IRuntimeEnvironment parent = CreateEnvironmentViewCore(
                parentReference.EnvironmentId,
                parentReference.Namespace,
                transaction,
                state,
                visited);
            if (parentReference.OverrideResolutionTags.Count > 0)
            {
                parent = parent.WithOverrideResolutionTags(parentReference.OverrideResolutionTags);
            }
            environment = InheritedRuntimeEnvironment.CreateTransactionView(
                environmentId,
                node,
                parent,
                _syntaxFactory,
                ns,
                _environments,
                transaction);
        }
        else
        {
            environment = RuntimeEnvironment.CreateTransactionView(
                environmentId,
                node,
                _syntaxFactory,
                ns,
                _environments,
                transaction);
        }
        visited.Remove(environmentId);
        return environment;
    }

    private void EnsureEnvironmentExists(IRuntimeEnvironment environment, ExecutionTransaction transaction)
    {
        RuntimeEnvironmentId environmentId = GetEnvironmentId(environment);
        RuntimeEnvironmentTransactionState state = GetState(transaction);
        if (state.ContainsEnvironment(environmentId))
        {
            return;
        }

        RuntimeEnvironmentParent? parent = null;
        if (environment is InheritedRuntimeEnvironment inheritedEnvironment)
        {
            EnsureEnvironmentExists(inheritedEnvironment.Parent, transaction);
            parent = CreateParentReference(inheritedEnvironment.Parent);
        }
        ITaggedStringConversionObserver? conversionObserver = environment is EnvironmentLike environmentLike
            ? environmentLike.TaggedStringConversionObserver
            : null;
        RuntimeEnvironmentNode node = new(
            environment.Name,
            environment.IsTransient,
            parent,
            conversionObserver);
        state.AddEnvironment(environmentId, node, environment);
    }

    private bool TryGetEnvironment(string name, [NotNullWhen(true)] out IRuntimeEnvironment? environment)
    {
        if (Environment.Name.Equals(name, StringComparison.Ordinal))
        {
            environment = Environment;
            return true;
        }
        if (_parent is not null && _parent.TryGetEnvironment(name, out environment))
        {
            environment = BindEnvironment(environment);
            return true;
        }
        RuntimeEnvironmentTransactionState state = GetState(_transaction);
        if (state.TryGetRegisteredEnvironment(name, out RuntimeEnvironmentId environmentId))
        {
            environment = CreateEnvironmentView(environmentId, UNBOUND_ENVIRONMENT, overrideResolutionTags: []);
            return true;
        }
        environment = null;
        return false;
    }

    private RuntimeEnvironmentTransactionState GetState(ExecutionTransaction transaction) =>
        transaction.GetParticipantState(_environments);

    private static RuntimeEnvironmentParent CreateParentReference(IRuntimeEnvironment environment) =>
        new(GetEnvironmentId(environment), environment.Namespace, environment.OverrideResolutionTags);

    private static RuntimeEnvironmentId GetEnvironmentId(IRuntimeEnvironment environment)
    {
        if (environment is not ITransactionalRuntimeEnvironment transactionalEnvironment)
        {
            throw new InvalidOperationException($"Runtime environment type '{environment.GetType().FullName}' does not expose transactional environment identity.");
        }
        return transactionalEnvironment.EnvironmentId;
    }
}
