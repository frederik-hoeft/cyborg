using Cyborg.Core.Modules.Runtime.Environments;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Runtime;

public sealed class RootModuleRuntime(GlobalRuntimeEnvironment defaultEnvironment, ILoggerFactory loggerFactory, IServiceProvider? serviceProvider = null)
    : ModuleRuntimeBase(RuntimeEnvironmentContext.CreateRoot(defaultEnvironment, loggerFactory), loggerFactory, serviceProvider)
{
    private protected override ModuleRuntimeBase Root => this;

    private protected override ModuleRuntimeBase? Parent => null;
}
