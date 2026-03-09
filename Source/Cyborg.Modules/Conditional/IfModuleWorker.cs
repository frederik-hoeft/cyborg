using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Modules.Conditional;

public sealed class IfModuleWorker(IfModule module) : ModuleWorker<IfModule>(module)
{
    protected async override Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        // condition doesn't need a full environment, just run it in the current one (use the context of the if statement)
        bool conditionResult = await runtime.ExecuteAsync(Module.Condition.Module, runtime.Environment, cancellationToken);
        ModuleContext branchToExecute = conditionResult ? Module.Then : Module.Else ?? Module.Then;
        return await runtime.ExecuteAsync(branchToExecute, cancellationToken);
    }
}
