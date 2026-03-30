namespace Cyborg.Core.Configuration;

public interface IConfigurationLoader
{
    Task AddSourceAsync(IConfiguration configuration, string configurationFilePath, CancellationToken cancellationToken);
}