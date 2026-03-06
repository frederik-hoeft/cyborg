using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Modules.Named;

public sealed class NamedModuleReferenceModuleWorker(NamedModuleReferenceModule module, IModuleRegistry moduleRegistry) : ModuleWorker<NamedModuleReferenceModule>(module)
{
    protected override Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (!moduleRegistry.TryGetModule(Module.Target, out ModuleContext? targetModule))
        {
            throw new InvalidOperationException($"Failed to find target module with name '{Module.Target}'.");
        }
        return runtime.ExecuteAsync(targetModule, cancellationToken);
    }
}
