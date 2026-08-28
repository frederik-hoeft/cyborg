using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Text;
using Cyborg.Core.Text.Rendering;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Switch;

public sealed class SwitchModuleWorker(IWorkerContext<SwitchModule> context, IModuleConfigurationLoader configurationLoader, ITaggedStringRenderer taggedStringRenderer) : ModuleWorker<SwitchModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        if (!runtime.Environment.TryResolveVariable(Module.Variable, out TaggedString caseName))
        {
            Logger.LogSwitchVariableNotFound(Module.Variable);
            throw new InvalidOperationException("Failed to resolve case from environment.");
        }
        string caseNameValue = caseName.Value;
        string renderedCaseName = taggedStringRenderer.Render(caseName);
        if (!Module.Cases.ToDictionary(static t => t.Name, static t => t.Path).TryGetValue(caseNameValue, out string? templatePath))
        {
            Logger.LogSwitchCaseNotFound(renderedCaseName);
            throw new InvalidOperationException($"Template '{renderedCaseName}' not found in cases.");
        }
        Logger.LogSwitchCaseSelected(renderedCaseName, templatePath);
        // Load the case content from the specified path
        ModuleContext module = await configurationLoader.LoadModuleAsync(templatePath, cancellationToken);
        IModuleExecutionResult result = await runtime.ExecuteAsync(module, cancellationToken);
        return runtime.Exit(WithStatus(result.Status));
    }
}
