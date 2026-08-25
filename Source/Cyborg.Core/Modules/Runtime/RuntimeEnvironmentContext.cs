using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using Cyborg.Core.Text;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Runtime;

internal sealed class RuntimeEnvironmentContext
{
    private const string UNBOUND_ENVIRONMENT = "__UNBOUND";

    private readonly RuntimeEnvironmentCatalog _catalog;
    private readonly ILogger _logger;
    private readonly RuntimeEnvironmentContext? _parent;

    public IRuntimeEnvironment GlobalEnvironment { get; }

    public IRuntimeEnvironment ParentEnvironment => _parent?.Environment ?? GlobalEnvironment;

    public IRuntimeEnvironment Environment { get; }

    public VariableSyntaxBuilder SyntaxFactory => GlobalEnvironment.SyntaxFactory;

    private RuntimeEnvironmentContext(
        RuntimeEnvironmentCatalog catalog,
        ILogger logger,
        RuntimeEnvironmentContext? parent,
        IRuntimeEnvironment globalEnvironment,
        IRuntimeEnvironment environment)
    {
        _catalog = catalog;
        _logger = logger;
        _parent = parent;
        GlobalEnvironment = globalEnvironment;
        Environment = environment;
    }

    public static RuntimeEnvironmentContext CreateRoot(GlobalRuntimeEnvironment globalEnvironment, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(globalEnvironment);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ILogger logger = loggerFactory.CreateLogger("cyborg.core.runtime");
        return new RuntimeEnvironmentContext(new RuntimeEnvironmentCatalog(), logger, parent: null, globalEnvironment, globalEnvironment);
    }

    public RuntimeEnvironmentContext CreateChild(IRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return new RuntimeEnvironmentContext(_catalog, _logger, this, GlobalEnvironment, environment);
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
        IRuntimeEnvironment environment = scope switch
        {
            EnvironmentScope.Isolated => new RuntimeEnvironment(name, transient, SyntaxFactory, UNBOUND_ENVIRONMENT)
            {
                TaggedStringConversionObserver = conversionObserver
            },
            EnvironmentScope.Global => GlobalEnvironment,
            EnvironmentScope.InheritParent => new InheritedRuntimeEnvironment(name, Environment, transient, SyntaxFactory, UNBOUND_ENVIRONMENT)
            {
                TaggedStringConversionObserver = conversionObserver
            },
            EnvironmentScope.InheritGlobal => new InheritedRuntimeEnvironment(name, GlobalEnvironment, transient, SyntaxFactory, UNBOUND_ENVIRONMENT)
            {
                TaggedStringConversionObserver = conversionObserver
            },
            EnvironmentScope.Parent or EnvironmentScope.Current => Environment.Bind(UNBOUND_ENVIRONMENT),
            EnvironmentScope.Reference => throw new ArgumentException("Attempting to create an environment by reference without providing an environment reference.", nameof(scope)),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Invalid environment scope.")
        };
        _logger.LogEnvironmentCreated(scope.ToString(), environment.Name);
        _catalog.TryAdd(environment);
        return environment;
    }

    private bool TryGetEnvironment(string name, [NotNullWhen(true)] out IRuntimeEnvironment? environment)
    {
        if (Environment.Name.Equals(name, StringComparison.Ordinal))
        {
            environment = Environment;
            return true;
        }
        if (_parent is not null)
        {
            return _parent.TryGetEnvironment(name, out environment);
        }
        return _catalog.TryGet(name, out environment);
    }
}
