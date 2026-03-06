using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Modules.Named;

public sealed class NamedModuleDefinitionModuleWorker(NamedModuleDefinitionModule module) : ModuleWorker<NamedModuleDefinitionModule>(module)
{
    protected override Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return runtime.ExecuteAsync(Module, cancellationToken);
    }
}
