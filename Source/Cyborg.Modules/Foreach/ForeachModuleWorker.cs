using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Artifacts;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Foreach;

public sealed class ForeachModuleWorker(IWorkerContext<ForeachModule> context) : ModuleWorker<ForeachModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        if (!runtime.Environment.TryResolveVariable(Module.Collection, out IEnumerable<object>? collection))
        {
            Logger.LogCollectionNotFound(Module.Collection);
            throw new InvalidOperationException($"Collection variable '{Module.Collection}' not found in the current environment.");
        }
        ModuleExitStatus exitCode = ModuleExitStatus.Skipped;
        int iteration = 0;
        foreach (object item in collection)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Logger.LogForeachCanceled(iteration);
                return runtime.Exit(Canceled());
            }
            Logger.LogForeachIteration(++iteration, Module.ItemVariable);
            IRuntimeEnvironment loopEnvironment = runtime.PrepareEnvironment(Module.Body);
            if (item is IDecomposable decomposable)
            {
                loopEnvironment.Publish(Module.ItemVariable, decomposable, DecompositionStrategy.FullHierarchy, publishNullValues: true);
            }
            else
            {
                loopEnvironment.SetVariable(Module.ItemVariable, item);
            }
            IModuleExecutionResult result = await runtime.ExecuteAsync(Module.Body, loopEnvironment, cancellationToken);
            if (result.Status is ModuleExitStatus.Canceled)
            {
                Logger.LogForeachCanceled(iteration);
                return runtime.Exit(Canceled());
            }
            if (result.Status is ModuleExitStatus.Failed)
            {
                if (!Module.ContinueOnError)
                {
                    Logger.LogForeachIterationFailedAborting(iteration);
                    return runtime.Exit(Failed());
                }
                Logger.LogForeachIterationFailedContinuing(iteration);
                exitCode = ModuleExitStatus.Failed;
            }
            else if (result.Status is ModuleExitStatus.Success && exitCode is ModuleExitStatus.Skipped)
            {
                exitCode = ModuleExitStatus.Success;
            }
        }
        Logger.LogForeachCompleted(iteration, exitCode.ToString());
        return runtime.Exit(WithStatus(exitCode));
    }
}
