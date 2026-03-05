using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Modules.Sequence;

// sample sequence module worker that executes each step in order and returns false if any step fails
public sealed class SequenceModuleWorker(SequenceModule module) : ModuleWorker<SequenceModule>(module)
{
    protected async override Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        foreach (ModuleContext step in Module.Steps)
        {
            bool success = await runtime.ExecuteAsync(step, cancellationToken);
            if (!success)
            {
                return false;
            }
        }
        return true;
    }
}
