using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.TestAdapter;

public static class TestServiceConfiguration
{
    public static IServiceCollection CreateDefaultServices() => new ServiceCollection();
}
