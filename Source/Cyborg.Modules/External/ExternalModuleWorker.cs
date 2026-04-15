using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.External;

public sealed class ExternalModuleWorker(IWorkerContext<ExternalModule> context, IModuleConfigurationLoader configurationLoader) : ModuleWorker<ExternalModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ModuleContext moduleContext = await configurationLoader.LoadModuleAsync(Module.Path, cancellationToken);
        IModuleExecutionResult executionResult = await runtime.ExecuteAsync(moduleContext, cancellationToken);
        return runtime.Exit(WithStatus(executionResult.Status));
    }
}