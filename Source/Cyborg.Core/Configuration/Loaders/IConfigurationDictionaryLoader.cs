namespace Cyborg.Core.Configuration.Loaders;

public interface IConfigurationDictionaryLoader : IConfigurationLoader
{
    IConfigurationDictionaryLoader AddEntry<T>(string key, T value);
}
