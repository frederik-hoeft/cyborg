using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Configuration.Serialization;

public sealed class DefaultJsonLoaderContextProvider(IServiceProvider serviceProvider) : IJsonLoaderContextProvider
{
    public IJsonLoaderContext GetContext() => serviceProvider.GetRequiredService<IJsonLoaderContext>();
}
