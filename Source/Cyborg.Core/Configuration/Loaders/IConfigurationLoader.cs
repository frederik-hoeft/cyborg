namespace Cyborg.Core.Configuration.Loaders;

public interface IConfigurationLoader
{
    IAsyncEnumerable<IConfigurationSource> LoadSourcesAsync(CancellationToken cancellationToken);
}
