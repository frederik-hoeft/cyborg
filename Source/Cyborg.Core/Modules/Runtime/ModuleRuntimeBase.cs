using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;

namespace Cyborg.Core.Modules.Runtime;

public abstract class ModuleRuntimeBase(VariableSyntaxBuilder syntaxFactory) : IModuleRuntime
{
    private const string UNBOUND_ENVIRONMENT = "__UNBOUND";

    public abstract IRuntimeEnvironment GlobalEnvironment { get; }

    public abstract IRuntimeEnvironment ParentEnvironment { get; }

    public abstract IRuntimeEnvironment Environment { get; }

    protected abstract IModuleRuntime? Parent { get; }

    protected VariableSyntaxBuilder SyntaxFactory => syntaxFactory;

    protected IRuntimeEnvironment CreateScopedEnvironment(IModuleRuntime parent, EnvironmentScope scope, string? name, bool transient = false)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (string.IsNullOrEmpty(name))
        {
            transient = true;
            name = Guid.CreateVersion7().ToString();
        }
        IRuntimeEnvironment environment = scope switch
        {
            EnvironmentScope.Isolated => new RuntimeEnvironment(name, transient, SyntaxFactory, UNBOUND_ENVIRONMENT),
            EnvironmentScope.Global => parent.GlobalEnvironment,
            EnvironmentScope.InheritParent => new InheritedRuntimeEnvironment(name, parent.Environment, transient, SyntaxFactory, UNBOUND_ENVIRONMENT),
            EnvironmentScope.InheritGlobal => new InheritedRuntimeEnvironment(name, parent.GlobalEnvironment, transient, SyntaxFactory, UNBOUND_ENVIRONMENT),
            EnvironmentScope.Parent or EnvironmentScope.Current => parent.Environment.Bind(UNBOUND_ENVIRONMENT),
            EnvironmentScope.Reference => throw new ArgumentException("Attempting to create an environment by reference without providing an environment reference.", nameof(scope)),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Invalid environment scope.")
        };
        parent.TryAddEnvironment(environment);
        return environment;
    }

    public IRuntimeEnvironment PrepareEnvironment(ModuleContext moduleContext, IReadOnlyCollection<string>? overrideResolutionTags = null) =>
        PrepareEnvironment(moduleContext?.Environment ?? ModuleEnvironment.Default, overrideResolutionTags);

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
        }
        environment ??= CreateScopedEnvironment(parent: this, moduleEnvironment.Scope, moduleEnvironment.Name, moduleEnvironment.Transient);
        if (overrideResolutionTags is not null)
        {
            foreach (string tag in overrideResolutionTags)
            {
                if (!SyntaxFactory.IsValidIdentifier(tag))
                {
                    throw new InvalidOperationException($"Override resolution tags must be valid identifiers: \"{tag}\"");
                }
            }
            environment = environment.WithOverrideResolutionTags(overrideResolutionTags);
        }
        return environment;
    }

    public virtual Task<IModuleExecutionResult> ExecuteAsync(ModuleContext moduleContext, CancellationToken cancellationToken = default)
    {
        IRuntimeEnvironment environment = PrepareEnvironment(moduleContext);
        return ExecuteAsync(moduleContext, environment, cancellationToken);
    }

    public virtual async Task<IModuleExecutionResult> ExecuteAsync(ModuleContext moduleContext, IRuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleContext);
        ArgumentNullException.ThrowIfNull(environment);
        if (moduleContext.Requires is { ArgumentNamespace: var ns, Arguments: { Count: > 0 } args })
        {
            List<string> errors = [];
            List<(string Argument, object Value)> resolvedArguments = [];
            bool isNamespaced = !string.IsNullOrEmpty(ns);
            if (!string.IsNullOrEmpty(ns) && !SyntaxFactory.IsValidIdentifier(ns))
            {
                errors.Add($"Template namespaces must be valid identifiers: '{ns}'");
            }
            int i = -1;
            foreach (string arg in args)
            {
                ++i;
                if (!SyntaxFactory.IsValidIdentifier(arg))
                {
                    errors.Add($"Template argument names must be valid identifiers: argv[{i}] = '{arg}'");
                    continue;
                }
                PathSyntax pathSyntax = isNamespaced ? SyntaxFactory.Path(ns!, arg) : SyntaxFactory.Path(arg);
                if (environment.TryResolveVariable(pathSyntax, out object? value))
                {
                    resolvedArguments.Add((pathSyntax, value));
                    continue;
                }
                errors.Add($"Unable to resolve template argument: '{arg}'");
            }
            if (errors.Count > 0)
            {
                throw new InvalidOperationException($"Module execution failed due to missing required arguments:{System.Environment.NewLine}    {string.Join($"{System.Environment.NewLine}    ", errors)}");
            }
            // normalize resolved arguments to be unqualified by the template namespace, since they are now scoped to the current namespace and should be easily accessible
            foreach ((string argument, object value) in resolvedArguments)
            {
                environment.SetVariable(argument, value);
            }
        }
        if (moduleContext.Configuration is { } configuration)
        {
            await ExecuteAsync(configuration.Module, environment, cancellationToken);
        }
        return await ExecuteAsync(moduleContext.Module.Module, environment, cancellationToken);
    }

    public abstract Task<IModuleExecutionResult> ExecuteAsync(IModuleWorker module, EnvironmentScope scope = EnvironmentScope.Global, string? name = null, CancellationToken cancellationToken = default);

    public abstract Task<IModuleExecutionResult> ExecuteAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken = default);

    public abstract bool TryAddEnvironment(IRuntimeEnvironment environment);

    public abstract bool TryGetEnvironment(string name, [NotNullWhen(true)] out IRuntimeEnvironment? environment);

    public abstract bool TryRemoveEnvironment(IRuntimeEnvironment environment);

    public virtual IModuleExecutionResult Exit<TModule>(IModuleExecutionResult<TModule> result) where TModule : ModuleBase, IModule
    {
        ArgumentNullException.ThrowIfNull(result);
        // When specifying scopes during configuration, the caller expects those scopes to be relative to the actual parent module,
        // since this internal runtime nesting for artifact publication is an implementation detail that should not affect configuration.
        IEnvironmentLike artifacts = result.Artifacts.Build(result.Status);
        IModuleRuntime responsibleRuntime = Parent ?? this;
        ModuleEnvironment deploymentTarget = result.Module.Artifacts.Environment;
        IRuntimeEnvironment targetEnvironment = responsibleRuntime.PrepareEnvironment(deploymentTarget);
        targetEnvironment.Publish(artifacts);
        return new ModuleExecutionResult(result.Module, result.Status, artifacts);
    }

    public virtual IRuntimeEnvironment? ResolveEnvironmentReference(ModuleEnvironmentReference environmentReference) => environmentReference switch
    {
        { Scope: EnvironmentScopeReference.Current } => Environment,
        { Scope: EnvironmentScopeReference.Global } => GlobalEnvironment,
        { Scope: EnvironmentScopeReference.Parent } => ParentEnvironment,
        { Scope: EnvironmentScopeReference.Reference, Name: { } name } when TryGetEnvironment(name, out IRuntimeEnvironment? environment) => environment,
        _ => null
    };
}