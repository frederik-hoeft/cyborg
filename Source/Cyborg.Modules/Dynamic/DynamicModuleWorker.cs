using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Dynamic;

public sealed class DynamicModuleWorker(IWorkerContext<DynamicModule> context) : ModuleWorker<DynamicModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        IModuleExecutionResult result = await runtime.ExecuteAsync(Module.Body, cancellationToken);
        return runtime.Exit(WithStatus(result.Status));
    }
}