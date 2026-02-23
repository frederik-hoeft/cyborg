namespace Cyborg.Core.Modules.Configuration;

public interface IModuleConfigurationLoader
{
    Task<IModuleWorker> LoadModuleAsync(string configurationFilePath, CancellationToken cancellationToken);
}