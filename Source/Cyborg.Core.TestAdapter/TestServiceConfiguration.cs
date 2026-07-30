using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.TestAdapter;

internal static class TestServiceConfiguration
{
    public static IServiceCollection CreateDefaultServices() => new ServiceCollection();
}
