using Cyborg.Core.Configuration.Loaders;

namespace Cyborg.Core.Configuration.Builders;

public interface IConfigurationBuilder
{
    IServiceProvider ServiceProvider { get; }

    IConfigurationBuilder AddSource(IConfigurationLoader loader);

    IConfigurationBuilder Ignore(string key);

    ValueTask ApplyToAsync(IConfiguration configuration, CancellationToken cancellationToken);
}
