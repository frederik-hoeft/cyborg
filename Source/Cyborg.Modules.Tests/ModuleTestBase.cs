using Cyborg.Core.TestAdapter;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Modules.Tests;

public abstract class ModuleTestBase : CyborgTestBase
{
    protected override void ConfigureServices(IServiceCollection services, IJabServiceDiscovery jabServiceDiscovery)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(jabServiceDiscovery);
        base.ConfigureServices(services, jabServiceDiscovery);

        jabServiceDiscovery.RegisterFromModule<ICyborgModuleServices>(services);
    }
}
