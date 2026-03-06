using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using System.Collections.Frozen;

namespace Cyborg.Modules.Template;

public sealed class TemplateModuleWorker
(
    TemplateModule module,
    GlobalRuntimeEnvironment defaultEnvironment,
    IModuleConfigurationLoader configurationLoader
) : ModuleWorker<TemplateModule>(module)
{
    private readonly FrozenDictionary<string, string> _templateRegistry = module.Templates.ToFrozenDictionary(t => t.Name, t => t.Path);

    protected async override Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (!defaultEnvironment.TryResolveVariable(TemplateModule.LoadTargetName, out string? templateName))
        {
            throw new InvalidOperationException("Failed to resolve template name from environment.");
        }
        if (!_templateRegistry.TryGetValue(templateName, out string? templatePath))
        {
            throw new InvalidOperationException($"Template '{templateName}' was not found in the module template registry.");
        }
        // Load the template content from the specified path
        ModuleContext module = await configurationLoader.LoadModuleAsync(templatePath, cancellationToken);
        return await runtime.ExecuteAsync(module, cancellationToken);
    }
}
