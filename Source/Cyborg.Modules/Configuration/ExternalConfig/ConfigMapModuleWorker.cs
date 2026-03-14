using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Configuration.ExternalConfig;

public sealed class ExternalConfigModuleWorker(IWorkerContext<ExternalConfigModule> context, IModuleConfigurationLoader configurationLoader) : ModuleWorker<ExternalConfigModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ModuleContext moduleContext = await configurationLoader.LoadModuleAsync(Module.Path, cancellationToken);
        IModuleExecutionResult executionResult = await runtime.ExecuteAsync(moduleContext.Module.Module, runtime.Environment, cancellationToken);
        return runtime.Exit(WithStatus(executionResult.Status));
    }
}