using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Configuration.ConfigMap;

public sealed class ConfigMapModuleWorker(IWorkerContext<ConfigMapModule> context) : ModuleWorker<ConfigMapModule>(context)
{
    protected override Task<bool> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        foreach (DynamicKeyValuePair entry in Module.Entries)
        {
            runtime.Environment.SetVariable(entry.Key, entry.Value);
        }
        return Task.FromResult(true);
    }
}