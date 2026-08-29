using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Model;
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
            if (source.Definition is not IConfigurationModule)
            {
                throw new InvalidOperationException($"Module {source.ModuleId} is not a valid configuration source.");
            }
            IModuleExecutionResult result = await runtime.ExecuteAsync(source, runtime.Environment, cancellationToken);
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
