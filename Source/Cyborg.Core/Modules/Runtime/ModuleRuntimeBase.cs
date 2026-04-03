using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Runtime;

public abstract class ModuleRuntimeBase(VariableSyntaxBuilder syntaxFactory, ILoggerFactory loggerFactory) : IModuleRuntime
{
    private const string UNBOUND_ENVIRONMENT = "__UNBOUND";

    protected ILoggerFactory LoggerFactory { get; } = loggerFactory;

    protected ILogger Logger { get; } = loggerFactory.CreateLogger("cyborg.core.runtime");

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
        Logger.LogEnvironmentCreated(scope.ToString(), environment.Name);
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
            Logger.LogNamedEnvironmentResolved(moduleEnvironment.Name);
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
            Logger.LogOverrideTagsApplied(string.Join(", ", overrideResolutionTags), environment.Name);
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
            string argumentNamespace = ns ?? "(none)";
            Logger.LogTemplateArgumentsResolving(args.Count, moduleContext.Module.Module.ModuleId, argumentNamespace);
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
                PathSyntax path = SyntaxFactory.Path(arg);
                PathSyntax argumentPath = SyntaxFactory.Path(ns).Child(path);
                if (environment.TryResolveVariable(argumentPath, out object? value) || environment.TryResolveVariable(path, out value))
                {
                    resolvedArguments.Add((arg, value));
                    continue;
                }
                errors.Add($"Unable to resolve template argument: '{arg}'");
            }
            if (errors.Count > 0)
            {
                string errorMessage = $"Module execution failed due to missing required arguments:{System.Environment.NewLine}    {string.Join($"{System.Environment.NewLine}    ", errors)}";
                Logger.LogTemplateArgumentResolutionFailed(moduleContext.Module.Module.ModuleId, errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
            // normalize resolved arguments to be unqualified by the template namespace, since they are now scoped to the current namespace and should be easily accessible
            foreach ((string argument, object value) in resolvedArguments)
            {
                environment.SetVariable(argument, value);
            }
        }
        if (moduleContext.Configuration is { } configuration)
        {
            Logger.LogConfigurationModuleRunning(configuration.Module.ModuleId, moduleContext.Module.Module.ModuleId);
            IModuleExecutionResult result = await ExecuteAsync(configuration.Module, environment, cancellationToken);
            if (result.Status is ModuleExitStatus.Failed or ModuleExitStatus.Canceled)
            {
                Logger.LogModuleConfigurationFailed(configuration.Module.ModuleId, result.Status.ToString(), moduleContext.Module.Module.ModuleId, environment.Name);
                return new ModuleExecutionResult(moduleContext.Module.Module.Module, ModuleExitStatus.Failed, environment.CreateArtifactCollection());
            }
        }
        return await ExecuteAsync(moduleContext.Module.Module, environment, cancellationToken);
    }

    public abstract Task<IModuleExecutionResult> ExecuteAsync(IModuleWorker module, EnvironmentScope scope = EnvironmentScope.Global, string? name = null, CancellationToken cancellationToken = default);

    public abstract Task<IModuleExecutionResult> ExecuteAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken = default);

    protected async Task<IModuleExecutionResult> ExecuteModuleAsync(IModuleRuntime root, IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(environment);

        IRuntimeEnvironment boundEnvironment = environment.Bind(module);
        IModuleRuntime runtime = new ScopedRuntime(root, parent: this, boundEnvironment, SyntaxFactory, LoggerFactory);
        Logger.LogModuleDispatched(module.ModuleId, boundEnvironment.Name);
        try
        {
            IModuleExecutionResult result = await module.ExecuteAsync(runtime, cancellationToken);
            if (result.Status is ModuleExitStatus.Failed or ModuleExitStatus.Canceled)
            {
                Logger.LogModuleExecutionFailed(module.ModuleId, result.Status.ToString(), boundEnvironment.Name);
            }
            else
            {
                Logger.LogModuleCompleted(module.ModuleId, result.Status.ToString(), boundEnvironment.Name);
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            Logger.LogModuleCanceled(module.ModuleId, boundEnvironment.Name);
            return new ModuleExecutionResult(module.Module, ModuleExitStatus.Canceled, boundEnvironment.CreateArtifactCollection());
        }
        catch (Exception e)
        {
            Logger.LogModuleUnhandledException(module.ModuleId, boundEnvironment.Name, e);
            return new ModuleExecutionResult(module.Module, ModuleExitStatus.Failed, boundEnvironment.CreateArtifactCollection());
        }
    }

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
        Logger.LogArtifactPublishing(Environment.NamespaceOf(result.Module), TModule.ModuleId, deploymentTarget.Scope.ToString(), targetEnvironment.Name);
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