using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Dynamic;

public sealed class DynamicModuleWorker(IWorkerContext<DynamicModule> context) : ModuleWorker<DynamicModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        IRuntimeEnvironment environment = runtime.PrepareEnvironment(Module.Target, Module.Tags);
        IModuleExecutionResult result = await runtime.ExecuteAsync(Module.Target, environment, cancellationToken);
        return runtime.Exit(WithStatus(result.Status));
    }
}