using Cyborg.Core.Configuration.Loaders;
using System.Collections.Immutable;

namespace Cyborg.Core.Configuration.Builders;

public sealed class DefaultConfigurationBuilder(IServiceProvider serviceProvider) : IConfigurationBuilder
{
    private readonly List<IConfigurationLoader> _loaders = [];
    private readonly HashSet<string> _ignoredKeys = [];

    public IServiceProvider ServiceProvider => serviceProvider;

    public IConfigurationBuilder AddSource(IConfigurationLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        _loaders.Add(loader);
        return this;
    }

    public IConfigurationBuilder Ignore(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        _ignoredKeys.Add(key);
        return this;
    }

    public async ValueTask ApplyToAsync(IConfiguration configuration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.IsFinalized)
        {
            return;
        }
        ImmutableArray<IConfigurationLoader> loaders = [.. _loaders];
        List<IConfigurationSource> sources = [];
        foreach (IConfigurationLoader loader in loaders)
        {
            await foreach (IConfigurationSource source in loader.LoadSourcesAsync(cancellationToken))
            {
                sources.Add(source);
            }
        }
        configuration.FinalizeWith(sources, _ignoredKeys);
    }
}
