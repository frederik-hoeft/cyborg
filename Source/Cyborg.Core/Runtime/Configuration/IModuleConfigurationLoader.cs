namespace Cyborg.Core.Runtime.Configuration;

public interface IModuleConfigurationLoader
{
    Task<ModuleConfigurationLoadResult> LoadModuleAsync(string configurationFilePath, CancellationToken cancellationToken);
}
