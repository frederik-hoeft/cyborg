using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Cyborg.Core.Modules.Runtime;

public abstract class ModuleRuntimeBase(JsonNamingPolicy namingPolicy) : IModuleRuntime
{
    public abstract IRuntimeEnvironment GlobalEnvironment { get; }

    public abstract IRuntimeEnvironment ParentEnvironment { get; }

    public abstract IRuntimeEnvironment Environment { get; }

    protected JsonNamingPolicy NamingPolicy => namingPolicy;

    protected IRuntimeEnvironment CreateScopedEnvironment(IModuleRuntime parent, EnvironmentScope scope, string? name)
    {
        ArgumentNullException.ThrowIfNull(parent);
        bool transient = false;
        if (string.IsNullOrEmpty(name))
        {
            transient = true;
            name = Guid.CreateVersion7().ToString();
        }
        IRuntimeEnvironment environment = scope switch
        {
            EnvironmentScope.Isolated => new RuntimeEnvironment(name, transient, NamingPolicy),
            EnvironmentScope.Global => parent.GlobalEnvironment,
            EnvironmentScope.InheritParent => new InheritedRuntimeEnvironment(name, parent.Environment, transient, NamingPolicy),
            EnvironmentScope.InheritGlobal => new InheritedRuntimeEnvironment(name, parent.GlobalEnvironment, transient, NamingPolicy),
            EnvironmentScope.Parent or EnvironmentScope.Current => parent.Environment,
            EnvironmentScope.Reference => throw new ArgumentException("Attempting to create an environment by reference without providing an environment reference.", nameof(scope)),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Invalid environment scope.")
        };
        parent.TryAddEnvironment(environment);
        return environment;
    }

    public IRuntimeEnvironment PrepareEnvironment(ModuleContext moduleContext) => PrepareEnvironment(moduleContext?.Environment ?? ModuleEnvironment.Default);

    public IRuntimeEnvironment PrepareEnvironment(ModuleEnvironment moduleEnvironment)
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
        environment ??= CreateScopedEnvironment(parent: this, moduleEnvironment.Scope, moduleEnvironment.Name);
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

        if (result.Artifacts is { } artifacts)
        {
            ModuleEnvironment deploymentTarget = result.Module.Artifacts.Environment;
            IRuntimeEnvironment environment = PrepareEnvironment(deploymentTarget);
            artifacts.PublishToEnvironment(environment, result.Status);
        }
        return result;
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