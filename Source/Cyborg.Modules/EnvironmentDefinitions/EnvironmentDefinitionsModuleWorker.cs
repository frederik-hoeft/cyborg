using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Model;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.EnvironmentDefinitions;

public sealed class EnvironmentDefinitionsModuleWorker(IWorkerContext<EnvironmentDefinitionsModule> context) : ModuleWorker<EnvironmentDefinitionsModule>(context)
{
    protected override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        foreach (ModuleEnvironment environment in Module.Environments)
        {
            _ = runtime.PrepareEnvironment(environment);
        }
        return Task.FromResult(runtime.Exit(Success()));
    }
}
