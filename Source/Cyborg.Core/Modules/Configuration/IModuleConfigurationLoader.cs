using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Core.Modules.Configuration;

public interface IModuleConfigurationLoader
{
    Task<ModuleContext> LoadModuleAsync(string configurationFilePath, CancellationToken cancellationToken);
}