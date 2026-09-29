using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Retry;

public sealed class RetryModuleWorker(IWorkerContext<RetryModule> context) : ModuleWorker<RetryModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        int attempts = Module.Attempts;
        for (int attemptIndex = 0; attemptIndex < attempts; attemptIndex++)
        {
            int attempt = attemptIndex + 1;
            if (cancellationToken.IsCancellationRequested)
            {
                Logger.LogRetryCanceled(attempt, attempts);
                return runtime.Exit(Canceled());
            }

            Logger.LogRetryAttempt(attempt, attempts);
            IModuleExecutionResult result = await runtime.ExecuteAsync(Module.Body, cancellationToken);
            if (result.Status == ModuleExitStatus.Success)
            {
                Logger.LogRetrySucceeded(attempt, attempts);
                return runtime.Exit(Success());
            }

            if (result.Status == ModuleExitStatus.Canceled)
            {
                Logger.LogRetryCanceled(attempt, attempts);
                return runtime.Exit(Canceled());
            }

            Logger.LogRetryAttemptFailed(attempt, attempts, result.Status.ToString());
        }

        Logger.LogRetryExhausted(attempts);
        return runtime.Exit(Failed());
    }
}
