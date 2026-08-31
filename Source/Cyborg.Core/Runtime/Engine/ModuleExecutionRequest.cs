using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine;

/// <summary>
/// Describes one runtime-owned invocation before its transaction and DI scope are established.
/// </summary>
internal abstract class ModuleExecutionRequest(IRuntimeEnvironment environment, CancellationToken cancellationToken)
{
    public abstract string ModuleId { get; }

    public abstract IModule Module { get; }

    public IRuntimeEnvironment Environment { get; } = environment;

    public CancellationToken CancellationToken { get; } = cancellationToken;

    public abstract Task<IModuleExecutionResult> ExecuteInCurrentScopeAsync(IModuleExecutionRuntime runtime, IRuntimeEnvironment environment);
}

internal sealed class ModuleContextExecutionRequest(
    ModuleContext moduleContext,
    IRuntimeEnvironment environment,
    CancellationToken cancellationToken) : ModuleExecutionRequest(environment, cancellationToken)
{
    public override string ModuleId => moduleContext.Module.ModuleId;

    public override IModule Module => moduleContext.Module.Definition;

    public override Task<IModuleExecutionResult> ExecuteInCurrentScopeAsync(IModuleExecutionRuntime runtime, IRuntimeEnvironment environment) =>
        runtime.ExecuteModuleContextInCurrentScopeAsync(moduleContext, environment, CancellationToken);
}

internal sealed class LoadedConfigurationExecutionRequest(
    ModuleConfigurationLoadResult configuration,
    IRuntimeEnvironment environment,
    CancellationToken cancellationToken) : ModuleExecutionRequest(environment, cancellationToken)
{
    public override string ModuleId => configuration.ModuleContext.Module.ModuleId;

    public override IModule Module => configuration.ModuleContext.Module.Definition;

    public override Task<IModuleExecutionResult> ExecuteInCurrentScopeAsync(IModuleExecutionRuntime runtime, IRuntimeEnvironment environment) =>
        runtime.ExecuteLoadedConfigurationInCurrentScopeAsync(configuration, environment, CancellationToken);
}

internal sealed class LoadedRootModuleExecutionRequest(
    ModuleConfigurationLoadResult configuration,
    IRuntimeEnvironment environment,
    CancellationToken cancellationToken) : ModuleExecutionRequest(environment, cancellationToken)
{
    public override string ModuleId => configuration.ModuleContext.Module.ModuleId;

    public override IModule Module => configuration.ModuleContext.Module.Definition;

    public override Task<IModuleExecutionResult> ExecuteInCurrentScopeAsync(IModuleExecutionRuntime runtime, IRuntimeEnvironment environment) =>
        runtime.ExecuteLoadedRootModuleInCurrentScopeAsync(configuration, environment, CancellationToken);
}

internal sealed class ModuleReferenceExecutionRequest(
    ModuleReference moduleReference,
    IRuntimeEnvironment environment,
    CancellationToken cancellationToken) : ModuleExecutionRequest(environment, cancellationToken)
{
    public override string ModuleId => moduleReference.ModuleId;

    public override IModule Module => moduleReference.Definition;

    public override Task<IModuleExecutionResult> ExecuteInCurrentScopeAsync(IModuleExecutionRuntime runtime, IRuntimeEnvironment environment) =>
        runtime.ExecuteModuleReferenceInCurrentScopeAsync(moduleReference, environment, CancellationToken);
}

internal sealed class ActivatedWorkerExecutionRequest(
    IModuleWorker worker,
    IRuntimeEnvironment environment,
    CancellationToken cancellationToken) : ModuleExecutionRequest(environment, cancellationToken)
{
    public override string ModuleId => worker.ModuleId;

    public override IModule Module => worker.Module;

    public override Task<IModuleExecutionResult> ExecuteInCurrentScopeAsync(IModuleExecutionRuntime runtime, IRuntimeEnvironment environment) =>
        runtime.ExecuteActivatedWorkerInCurrentScopeAsync(worker, environment, CancellationToken);
}
