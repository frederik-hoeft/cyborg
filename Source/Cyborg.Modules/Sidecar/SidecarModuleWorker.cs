using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Model;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Sidecar;

public sealed class SidecarModuleWorker(IWorkerContext<SidecarModule> context) : ModuleWorker<SidecarModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        IReadOnlyList<ModuleContext> sidecars = Module.Sidecars ?? [];
        Logger.LogSidecarExecutionStarting(sidecars.Count);
        await using IConcurrentExecutionScope scope = runtime.OpenConcurrentExecution();
        IConcurrentModuleExecution primary = await scope.StartAsync(Module.Module, cancellationToken);
        IConcurrentModuleExecution[] sidecarExecutions = new IConcurrentModuleExecution[sidecars.Count];
        for (int i = 0; i < sidecars.Count; i++)
        {
            sidecarExecutions[i] = await scope.StartAsync(sidecars[i], cancellationToken);
        }

        Task<IModuleExecutionResult>[] watches = new Task<IModuleExecutionResult>[sidecarExecutions.Length + 1];
        watches[0] = WatchPrimaryAsync(primary, sidecarExecutions);
        for (int i = 0; i < sidecarExecutions.Length; i++)
        {
            watches[i + 1] = WatchSidecarAsync(i, sidecarExecutions[i], primary, sidecarExecutions);
        }

        IModuleExecutionResult[] results = await Task.WhenAll(watches);
        await scope.CloseAsync(cancellationToken);
        for (int i = 1; i < results.Length; i++)
        {
            if (results[i].Status == ModuleExitStatus.Failed)
            {
                return runtime.Exit(Failed());
            }
        }

        Logger.LogSidecarCompleted(results[0].Status.ToString());
        return runtime.Exit(WithStatus(results[0].Status));
    }

    private async Task<IModuleExecutionResult> WatchPrimaryAsync(IConcurrentModuleExecution primary, IReadOnlyList<IConcurrentModuleExecution> sidecars)
    {
        try
        {
            IModuleExecutionResult result = await primary.Completion;
            Logger.LogSidecarPrimaryCompleted(result.Status.ToString());
            return result;
        }
        finally
        {
            CancelAll(sidecars);
        }
    }

    private async Task<IModuleExecutionResult> WatchSidecarAsync(
        int sidecarIndex,
        IConcurrentModuleExecution sidecar,
        IConcurrentModuleExecution primary,
        IReadOnlyList<IConcurrentModuleExecution> sidecars)
    {
        try
        {
            IModuleExecutionResult result = await sidecar.Completion;
            if (result.Status == ModuleExitStatus.Failed)
            {
                Logger.LogSidecarFailed(sidecarIndex, result.Status.ToString());
                CancelAll(primary, sidecars);
            }
            return result;
        }
        catch (Exception exception)
        {
            Logger.LogSidecarFailed(sidecarIndex, exception.GetType().Name);
            CancelAll(primary, sidecars);
            throw;
        }
    }

    private static void CancelAll(IReadOnlyList<IConcurrentModuleExecution> executions)
    {
        for (int i = 0; i < executions.Count; i++)
        {
            executions[i].Cancel();
        }
    }

    private static void CancelAll(IConcurrentModuleExecution primary, IReadOnlyList<IConcurrentModuleExecution> sidecars)
    {
        primary.Cancel();
        CancelAll(sidecars);
    }
}
