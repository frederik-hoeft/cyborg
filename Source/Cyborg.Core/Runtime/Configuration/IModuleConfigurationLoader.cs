using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Configuration;

public interface IModuleConfigurationLoader
{
    Task<ModuleContext> LoadModuleAsync(string configurationFilePath, CancellationToken cancellationToken);
}
