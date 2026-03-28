using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Sequence;

// sample sequence module worker that executes each step in order and returns false if any step fails
public sealed class SequenceModuleWorker(IWorkerContext<SequenceModule> context) : ModuleWorker<SequenceModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ModuleExitStatus status = ModuleExitStatus.Skipped;
        foreach (ModuleContext step in Module.Steps)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return runtime.Exit(Canceled());
            }
            IModuleExecutionResult result = await runtime.ExecuteAsync(step, cancellationToken);
            if (result.Status is ModuleExitStatus.Canceled or ModuleExitStatus.Failed)
            {
                return runtime.Exit(WithStatus(result.Status));
            }
            if (result.Status is ModuleExitStatus.Success)
            {
                status = ModuleExitStatus.Success;
            }
        }
        return runtime.Exit(WithStatus(status));
    }
}
