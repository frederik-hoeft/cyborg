using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Switch;

public sealed class SwitchModuleWorker(IWorkerContext<SwitchModule> context, GlobalRuntimeEnvironment defaultEnvironment, IModuleConfigurationLoader configurationLoader) : ModuleWorker<SwitchModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        if (!defaultEnvironment.TryResolveVariable(Module.Variable, out string? caseName))
        {
            throw new InvalidOperationException("Failed to resolve case from environment.");
        }
        if (!Module.Cases.ToDictionary(static t => t.Name, static t => t.Path).TryGetValue(caseName, out string? templatePath))
        {
            throw new InvalidOperationException($"Template '{caseName}' not found in cases.");
        }
        // Load the case content from the specified path
        ModuleContext module = await configurationLoader.LoadModuleAsync(templatePath, cancellationToken);
        IModuleExecutionResult result = await runtime.ExecuteAsync(module, cancellationToken);
        return runtime.Exit(WithStatus(result.Status));
    }
}
