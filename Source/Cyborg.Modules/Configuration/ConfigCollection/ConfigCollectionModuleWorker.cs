using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Configuration.ConfigCollection;

public sealed class ConfigCollectionModuleWorker(IWorkerContext<ConfigCollectionModule> context) : ModuleWorker<ConfigCollectionModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ModuleExitStatus status = ModuleExitStatus.Skipped;
        foreach (ModuleReference source in Module.Sources)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return runtime.Exit(Canceled());
            }
            if (source.Module.Module is not IConfigurationModule)
            {
                throw new InvalidOperationException($"Module {source.Module.ModuleId} is not a valid configuration source.");
            }
            IModuleExecutionResult result = await runtime.ExecuteAsync(source.Module, runtime.Environment, cancellationToken);
            if (result.Status is ModuleExitStatus.Canceled or ModuleExitStatus.Failed)
            {
                return runtime.Exit(WithStatus(result.Status));
            }
            if (result.Status is ModuleExitStatus.Success)
            {
                status = ModuleExitStatus.Success;
            }
        }
        return runtime.Exit(WithStatus(status));
    }
}
