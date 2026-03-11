using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Conditional;

public sealed class IfModuleWorker(IWorkerContext<IfModule> context) : ModuleWorker<IfModule>(context)
{
    protected async override Task<bool> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        // condition doesn't need a full environment, just run it in the current one (use the context of the if statement)
        bool conditionResult = await runtime.ExecuteAsync(Module.Condition.Module, runtime.Environment, cancellationToken);
        ModuleContext branchToExecute = conditionResult ? Module.Then : Module.Else ?? Module.Then;
        return await runtime.ExecuteAsync(branchToExecute, cancellationToken);
    }
}
