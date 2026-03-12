using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Conditional;

public sealed class IfModuleWorker(IWorkerContext<IfModule> context) : ModuleWorker<IfModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        // condition doesn't need a full environment, just run it in the current one (use the context of the if statement)
        IModuleExecutionResult result = await runtime.ExecuteAsync(Module.Condition.Module, runtime.Environment, cancellationToken);
        if (result.Status is not ModuleExitStatus.Success)
        {
            // this was unexpected
            return runtime.Exit(WithStatus(result.Status));
        }
        if (!result.Artifacts.TryGetValue(IfModule.Target, out object? value) || value is not bool condition)
        {
            // this is not a valid result from the condition module
            return runtime.Exit(WithStatus(ModuleExitStatus.Failed));
        }
        ModuleContext branchToExecute = condition ? Module.Then : Module.Else ?? Module.Then;
        IModuleExecutionResult branchResult = await runtime.ExecuteAsync(branchToExecute, cancellationToken);
        return runtime.Exit(WithStatus(branchResult.Status));
    }
}
