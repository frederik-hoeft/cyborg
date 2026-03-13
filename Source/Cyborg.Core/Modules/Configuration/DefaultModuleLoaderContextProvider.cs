using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Modules.Configuration;

public sealed class DefaultModuleLoaderContextProvider(IServiceProvider serviceProvider) : IModuleLoaderContextProvider
{
    public IModuleLoaderContext GetContext() => serviceProvider.GetRequiredService<IModuleLoaderContext>();
}
