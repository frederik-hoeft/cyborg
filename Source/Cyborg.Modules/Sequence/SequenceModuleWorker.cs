using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.Sequence;

// sample sequence module worker that executes each step in order and returns false if any step fails
public sealed class SequenceModuleWorker(SequenceModule module) : ModuleWorker<SequenceModule>(module)
{
    public async override Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        foreach (ModuleReference step in Module.Steps)
        {
            bool success = await step.Module.ExecuteAsync(cancellationToken);
            if (!success)
            {
                return false;
            }
        }
        return true;
    }
}
