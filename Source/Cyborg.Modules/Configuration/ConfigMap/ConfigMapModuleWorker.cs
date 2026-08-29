using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Configuration.ConfigMap;

public sealed class ConfigMapModuleWorker(IWorkerContext<ConfigMapModule> context) : ModuleWorker<ConfigMapModule>(context)
{
    protected override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(runtime.Exit(Canceled()));
        }
        foreach (DynamicKeyValuePair entry in Module.Entries)
        {
            runtime.Environment.SetVariable(entry.Key, entry.Value);
        }
        return Task.FromResult(runtime.Exit(Success()));
    }
}
