using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Configuration.ConfigCollection;

public sealed class ConfigCollectionModuleWorker(IWorkerContext<ConfigCollectionModule> context) : ModuleWorker<ConfigCollectionModule>(context)
{
    protected async override Task<bool> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        foreach (ModuleReference source in Module.Sources)
        {
            if (source.Module is not IConfigurationModule)
            {
                throw new InvalidOperationException($"Module {source.Module.ModuleId} is not a valid configuration source.");
            }
            bool success = await runtime.ExecuteAsync(source.Module, runtime.Environment, cancellationToken);
            if (!success)
            {
                return runtime.Failure(Module);
            }
        }
        return runtime.Success(Module);
    }
}
