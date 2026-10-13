using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine;

/// <summary>
/// Dispatches targets inside an already-created invocation. Transaction forks, invocation lifecycle
/// notifications and DI scope ownership belong to <see cref="ModuleInvocationCoordinator"/>.
/// </summary>
internal sealed class ModuleTargetExecutor(
    IModuleRuntime runtime,
    IModuleRuntime root,
    RuntimeEnvironmentContext environmentContext,
    ModuleRuntimeServices runtimeServices,
    ActiveTransaction activeTransaction,
    IServiceProvider executionServices,
    ModuleInvocationContext invocationContext)
{
    public Task<IModuleExecutionResult> ExecuteAsync(ModuleExecutionTarget target, IRuntimeEnvironment environment)
    {
        // A loaded configuration seeds its registry within the child invocation before resolving
        // either its context or its root reference. Root execution deliberately skips context preparation.
        if (target.Configuration is { } configuration)
        {
            runtimeServices.ModuleRegistry.ApplySeed(activeTransaction.Current, configuration.RegistrySeed);
        }

        IRuntimeEnvironment scopedEnvironment = environmentContext.BindToCurrentTransaction(environment);
        return target.Kind switch
        {
            ModuleExecutionKind.Context => ExecuteContextAsync(target.Context!, scopedEnvironment, target.CancellationToken),
            ModuleExecutionKind.Configuration => ExecuteContextAsync(target.Configuration!.ModuleContext, scopedEnvironment, target.CancellationToken),
            ModuleExecutionKind.RootModule => ExecuteReferenceAsync(target.Configuration!.ModuleContext.Module, scopedEnvironment, target.CancellationToken),
            ModuleExecutionKind.Reference => ExecuteReferenceAsync(target.Reference!, scopedEnvironment, target.CancellationToken),
            ModuleExecutionKind.ActivatedWorker => ExecuteWorkerAsync(target.Worker!, scopedEnvironment, target.CancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
    }

    private async Task<IModuleExecutionResult> ExecuteContextAsync(ModuleContext moduleContext, IRuntimeEnvironment environment, CancellationToken cancellationToken)
    {
        IModuleExecutionResult? failure = await runtimeServices.ContextPreparer.PrepareAsync(runtime, moduleContext, environment, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }
        return await ExecuteReferenceAsync(moduleContext.Module, environment, cancellationToken);
    }

    private Task<IModuleExecutionResult> ExecuteReferenceAsync(ModuleReference reference, IRuntimeEnvironment environment, CancellationToken cancellationToken)
    {
        IModuleWorker worker = runtimeServices.Dispatcher.ActivateWorker(reference, executionServices);
        return ExecuteWorkerAsync(worker, environment, cancellationToken);
    }

    private Task<IModuleExecutionResult> ExecuteWorkerAsync(IModuleWorker worker, IRuntimeEnvironment environment, CancellationToken cancellationToken)
    {
        IRuntimeEnvironment boundEnvironment = environment.Bind(worker);
        RuntimeEnvironmentContext workerEnvironmentContext = environmentContext.CreateChildFrame(boundEnvironment);
        IModuleRuntime workerRuntime = new ScopedRuntime(root, workerEnvironmentContext, runtimeServices, activeTransaction, executionServices, invocationContext);
        return runtimeServices.Dispatcher.ExecuteAsync(worker, workerRuntime, boundEnvironment, executionServices, cancellationToken);
    }
}
