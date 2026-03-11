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
            EnvironmentScope.Parent => parent.Environment,
            EnvironmentScope.Reference => throw new ArgumentException("Attempting to create an environment by reference without providing an environment reference.", nameof(scope)),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Invalid environment scope.")
        };
        parent.TryAddEnvironment(environment);
        return environment;
    }

    public IRuntimeEnvironment PrepareEnvironment(ModuleContext moduleContext)
    {
        ArgumentNullException.ThrowIfNull(moduleContext);
        IRuntimeEnvironment? environment = null;
        if (moduleContext.Environment?.Scope is EnvironmentScope.Reference)
        {
            if (string.IsNullOrEmpty(moduleContext.Environment.Name))
            {
                throw new InvalidOperationException("Attempting to reference an environment without providing an environment name.");
            }
            if (!TryGetEnvironment(moduleContext.Environment.Name, out environment))
            {
                throw new InvalidOperationException($"Attempting to reference an environment that does not exist: {moduleContext.Environment.Name}");
            }
        }
        EnvironmentScope scope = moduleContext.Environment?.Scope ?? EnvironmentScope.Parent;
        environment ??= CreateScopedEnvironment(parent: this, scope, moduleContext.Environment?.Name);
        return environment;
    }

    public virtual Task<bool> ExecuteAsync(ModuleContext moduleContext, CancellationToken cancellationToken = default)
    {
        IRuntimeEnvironment environment = PrepareEnvironment(moduleContext);
        return ExecuteAsync(moduleContext, environment, cancellationToken);
    }

    public virtual async Task<bool> ExecuteAsync(ModuleContext moduleContext, IRuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleContext);
        if (moduleContext.Configuration is { } configuration)
        {
            await ExecuteAsync(configuration.Module, environment, cancellationToken);
        }
        return await ExecuteAsync(moduleContext.Module.Module, environment, cancellationToken);
    }

    public abstract Task<bool> ExecuteAsync(IModuleWorker module, EnvironmentScope scope = EnvironmentScope.Global, string? name = null, CancellationToken cancellationToken = default);

    public abstract Task<bool> ExecuteAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken = default);

    public abstract bool TryAddEnvironment(IRuntimeEnvironment environment);

    public abstract bool TryGetEnvironment(string name, [NotNullWhen(true)] out IRuntimeEnvironment? environment);

    public abstract bool TryRemoveEnvironment(IRuntimeEnvironment environment);

    public virtual void PublishArtifacts<TModule>(TModule module, IModuleArtifacts artifacts) where TModule : ModuleBase, IModule
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(artifacts);
        ModuleEnvironmentReference deploymentTarget = module.Artifacts.Environment;
        IRuntimeEnvironment environment = ResolveEnvironmentReference(deploymentTarget)
            ?? throw new InvalidOperationException($"Unable to resolve environment reference for module {module.Name}: {deploymentTarget}");
        artifacts.PublishToEnvironment(environment);
    }

    protected virtual bool Exit<TModule>(TModule module, bool success, IModuleArtifacts? artifacts) where TModule : ModuleBase, IModule
    {
        if (artifacts is not null)
        {
            PublishArtifacts(module, artifacts);
        }
        return success;
    }

    public bool Success<TModule>(TModule module, IModuleArtifacts? artifacts = null) where TModule : ModuleBase, IModule => Exit(module, success: true, artifacts);

    public bool Failure<TModule>(TModule module, IModuleArtifacts? artifacts = null) where TModule : ModuleBase, IModule => Exit(module, success: false, artifacts);

    public virtual IRuntimeEnvironment? ResolveEnvironmentReference(ModuleEnvironmentReference environmentReference) => environmentReference switch
    {
        { Scope: EnvironmentScopeReference.Current } => Environment,
        { Scope: EnvironmentScopeReference.Global } => GlobalEnvironment,
        { Scope: EnvironmentScopeReference.Parent } => ParentEnvironment,
        { Scope: EnvironmentScopeReference.Reference, Name: { } name } when TryGetEnvironment(name, out IRuntimeEnvironment? environment) => environment,
        _ => null
    };
}