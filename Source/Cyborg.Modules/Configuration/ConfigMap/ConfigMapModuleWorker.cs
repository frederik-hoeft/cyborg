using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Modules.Configuration.ConfigMap;

public sealed class ConfigMapModuleWorker(ConfigMapModule module) : ModuleWorker<ConfigMapModule>(module)
{
    protected override Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        foreach (DynamicKeyValuePair entry in Module.Entries)
        {
            runtime.Environment.SetVariable(entry.Key, entry.Value);
        }
        return Task.FromResult(true);
    }
}