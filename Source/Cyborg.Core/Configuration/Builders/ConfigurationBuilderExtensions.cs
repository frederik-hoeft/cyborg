using Cyborg.Core.Common.Extensions;
using Cyborg.Core.Configuration.Loaders;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Configuration.Builders;

[SuppressMessage("Design", CA1034, Justification = CA1034_JUSTIFY_EXTENSION_SYNTAX_CSHARP_14)]
public static class ConfigurationBuilderExtensions
{
    extension(IConfigurationBuilder self)
    {
        private IConfigurationBuilder AddLoader<TLoader>(Action<TLoader> configure) where TLoader : class, IConfigurationLoader
        {
            ArgumentNullException.ThrowIfNull(configure);
            TLoader loader = self.ServiceProvider.GetRequiredService<TLoader>();
            configure(loader);
            self.AddSource(loader);
            return self;
        }

        public IConfigurationBuilder AddFiles(Action<IConfigurationFileLoader> configure) => self.AddLoader(configure);

        public IConfigurationBuilder AddDictionary(Action<IConfigurationDictionaryLoader> configure) => self.AddLoader(configure);

        public IConfigurationBuilder AddDictionary<TValue>(IReadOnlyDictionary<string, TValue> dictionary)
        {
            ArgumentNullException.ThrowIfNull(dictionary);
            return self.AddLoader<IConfigurationDictionaryLoader>(dictionaryBuilder => dictionary.ForEach(kvp => dictionaryBuilder.AddEntry(kvp.Key, kvp.Value)));
        }
    }
}
