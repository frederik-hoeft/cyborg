using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Parallel;

public sealed class ParallelModuleWorker(IWorkerContext<ParallelModule> context) : ModuleWorker<ParallelModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        IReadOnlyList<IModuleExecutionResult> results = await runtime.ExecuteConcurrentlyAsync(Module.Branches, cancellationToken);
        ModuleExitStatus status = ModuleExitStatus.Skipped;
        foreach (IModuleExecutionResult result in results)
        {
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
