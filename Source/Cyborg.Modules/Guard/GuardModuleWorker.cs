using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Guard;

public sealed partial class GuardModuleWorker(IWorkerContext<GuardModule> context) : ModuleWorker<GuardModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        bool handledFailure = false;
        ModuleExitStatus? exitStatus = null;
        Logger.LogGuardTryExecuting(ModuleId);
        try
        {
            IModuleExecutionResult result = await runtime.ExecuteAsync(Module.Try, cancellationToken);
            if (result.Status == ModuleExitStatus.Failed)
            {
                // prevent double handling of failure in the case where the catch block also fails
                handledFailure = true;
                Logger.LogGuardCatchTriggeredByFailure(ModuleId, result.Status.ToString());
                IModuleExecutionResult<GuardModule> catchResult = await ExecuteCatchAsync(runtime, cancellationToken);
                exitStatus = catchResult.Status;
            }
        }
        // explicitly fail on cancellation (Ctrl-C, etc.)
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Logger.LogGuardCatchTriggeredByException(ModuleId, e.GetType().Name);
            if (handledFailure)
            {
                return runtime.Exit(Failed());
            }
            IModuleExecutionResult<GuardModule> catchResult = await ExecuteCatchAsync(runtime, cancellationToken);
            exitStatus = catchResult.Status;
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Logger.LogGuardFinallyExecuting(ModuleId);
                IModuleExecutionResult finallyResult = await runtime.ExecuteAsync(Module.Finally, cancellationToken);
                exitStatus ??= finallyResult.Status;
            }
            else
            {
                Logger.LogGuardFinallySkipped(ModuleId);
            }
        }
        exitStatus ??= ModuleExitStatus.Failed;
        Logger.LogGuardCompleted(ModuleId, exitStatus.Value.ToString());
        return runtime.Exit(WithStatus(exitStatus.Value));
    }

    private async ValueTask<IModuleExecutionResult<GuardModule>> ExecuteCatchAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        if (Module.Catch is not null)
        {
            IModuleExecutionResult catchResult = await runtime.ExecuteAsync(Module.Catch, cancellationToken);
            return WithStatus(catchResult.Status);
        }
        Logger.LogGuardNoCatchBlock(ModuleId, Module.Behavior.ToString());
        if (Module.Behavior is GuardModuleBehavior.Swallow)
        {
            return Success();
        }
        return Failed();
    }
}