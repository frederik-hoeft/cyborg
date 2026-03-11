using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Named;

public sealed class NamedModuleDefinitionModuleWorker(IWorkerContext<NamedModuleDefinitionModule> context) : ModuleWorker<NamedModuleDefinitionModule>(context)
{
    protected override Task<bool> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken) => runtime.ExecuteAsync(Module, cancellationToken);
}
