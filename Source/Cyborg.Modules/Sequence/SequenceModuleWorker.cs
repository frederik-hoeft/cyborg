using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Sequence;

// sample sequence module worker that executes each step in order and returns false if any step fails
public sealed class SequenceModuleWorker(IWorkerContext<SequenceModule> context) : ModuleWorker<SequenceModule>(context)
{
    protected async override Task<bool> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        foreach (ModuleContext step in Module.Steps)
        {
            bool success = await runtime.ExecuteAsync(step, cancellationToken);
            if (!success)
            {
                return runtime.Failure(Module);
            }
        }
        return runtime.Success(Module);
    }
}
