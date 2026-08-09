namespace Cyborg.Core.Configuration.Loaders;

public interface IConfigurationFileLoader : IConfigurationLoader
{
    IConfigurationFileLoader Add(string filePath);
}
