using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Modules.Configuration.ConfigCollection;

public sealed class ConfigCollectionModuleWorker(ConfigCollectionModule module) : ModuleWorker<ConfigCollectionModule>(module)
{
    protected async override Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        foreach (ModuleReference source in Module.Sources)
        {
            if (source.Module is not IConfigurationModule)
            {
                throw new InvalidOperationException($"Module {source.Module.ModuleId} is not a valid configuration source.");
            }
            bool success = await runtime.ExecuteAsync(source.Module, runtime.Environment, cancellationToken);
            if (!success)
            {
                return false;
            }
        }
        return true;
    }
}
