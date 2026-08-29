using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Empty;

public sealed class EmptyModuleWorker(IWorkerContext<EmptyModule> context) : ModuleWorker<EmptyModule>(context)
{
    protected override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken) => Task.FromResult(runtime.Exit(Success()));
}
