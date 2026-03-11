using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Template;

public sealed class TemplateModuleWorker
(
    IWorkerContext<TemplateModule> context,
    GlobalRuntimeEnvironment defaultEnvironment,
    IModuleConfigurationLoader configurationLoader
) : ModuleWorker<TemplateModule>(context)
{
    protected async override Task<bool> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        if (!defaultEnvironment.TryResolveVariable(TemplateModule.LoadTargetName, out string? templateName))
        {
            throw new InvalidOperationException("Failed to resolve template name from environment.");
        }
        if (!Module.Templates.ToDictionary(static t => t.Name, static t => t.Path).TryGetValue(templateName, out string? templatePath))
        {
            throw new InvalidOperationException($"Template '{templateName}' was not found in the module template registry.");
        }
        // Load the template content from the specified path
        ModuleContext module = await configurationLoader.LoadModuleAsync(templatePath, cancellationToken);
        return await runtime.ExecuteAsync(module, cancellationToken);
    }
}
