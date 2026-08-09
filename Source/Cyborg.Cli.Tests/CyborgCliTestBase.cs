using Cyborg.Core.TestAdapter;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Cli.Tests;

public abstract class CyborgCliTestBase : CyborgTestBase
{
    protected override void ConfigureServices(IServiceCollection services, IJabServiceDiscovery jabServiceDiscovery)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(jabServiceDiscovery);
        base.ConfigureServices(services, jabServiceDiscovery);

        jabServiceDiscovery.RegisterFromModule<DefaultServiceProvider>(services);
    }
}
