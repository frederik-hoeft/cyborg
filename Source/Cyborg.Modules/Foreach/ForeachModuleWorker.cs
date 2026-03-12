using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Artifacts;
using Cyborg.Core.Modules.Runtime.Environments;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Foreach;

public sealed class ForeachModuleWorker(IWorkerContext<ForeachModule> context) : ModuleWorker<ForeachModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        if (!runtime.Environment.TryResolveVariable(Module.Collection, out IEnumerable<object>? collection))
        {
            // TODO: proper failure result once we have logging
            throw new InvalidOperationException($"Collection variable '{Module.Collection}' not found in the current environment.");
        }
        ModuleExitStatus exitCode = ModuleExitStatus.Skipped;
        foreach (object item in collection)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                // TODO: logging
                return runtime.Exit(Canceled());
            }
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
                return runtime.Exit(Canceled());
            }
            if (result.Status is ModuleExitStatus.Failed)
            {
                if (!Module.ContinueOnError)
                {
                    return runtime.Exit(Failed());
                }
                exitCode = ModuleExitStatus.Failed;
            }
            else if (result.Status is ModuleExitStatus.Success && exitCode is ModuleExitStatus.Skipped)
            {
                exitCode = ModuleExitStatus.Success;
            }
        }
        return runtime.Exit(WithStatus(exitCode));
    }
}
