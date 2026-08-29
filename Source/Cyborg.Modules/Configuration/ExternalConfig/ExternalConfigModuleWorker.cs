using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Configuration.ExternalConfig;

public sealed class ExternalConfigModuleWorker(IWorkerContext<ExternalConfigModule> context, IModuleConfigurationLoader configurationLoader) : ModuleWorker<ExternalConfigModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ModuleConfigurationLoadResult configuration = await configurationLoader.LoadModuleAsync(Module.Path, cancellationToken);
        IModuleExecutionResult executionResult = await runtime.ExecuteRootModuleAsync(configuration, runtime.Environment, cancellationToken);
        return runtime.Exit(WithStatus(executionResult.Status));
    }
}
