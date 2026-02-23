using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Runtime;
using System.Collections.Frozen;

namespace Cyborg.Modules.Template;

public sealed class TemplateModuleWorker
(
    TemplateModule module,
    DefaultEnvironment defaultEnvironment,
    IModuleConfigurationLoader configurationLoader
) : ModuleWorker<TemplateModule>(module)
{
    private readonly FrozenDictionary<string, string> _templateRegistry = module.Templates.ToFrozenDictionary(t => t.Name, t => t.Path);

    public async override Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!defaultEnvironment.TryResolveVariable(TemplateModule.LoadTargetName, out string? templateName))
        {
            throw new InvalidOperationException("Failed to resolve template name from environment.");
        }
        if (!_templateRegistry.TryGetValue(templateName, out string? templatePath))
        {
            throw new InvalidOperationException($"Template '{templateName}' was not found in the module template registry.");
        }
        // Load the template content from the specified path
        IModuleWorker module = await configurationLoader.LoadModuleAsync(templatePath, cancellationToken);
        return await module.ExecuteAsync(cancellationToken);
    }
}
