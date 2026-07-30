using Cyborg.Core.TestAdapter;
using Cyborg.Modules.Borg;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Modules.Borg.Tests;

public abstract class BorgModuleTestBase : CyborgTestBase
{
    protected override void ConfigureServices(IServiceCollection services, IJabServiceDiscovery jabServiceDiscovery)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(jabServiceDiscovery);
        base.ConfigureServices(services, jabServiceDiscovery);

        jabServiceDiscovery.RegisterFromModule<ICyborgModuleServices>(services);
        jabServiceDiscovery.RegisterFromModule<ICyborgBorgServices>(services);
    }
}
