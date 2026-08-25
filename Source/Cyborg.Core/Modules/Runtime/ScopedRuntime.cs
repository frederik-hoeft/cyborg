using Cyborg.Core.Modules.Runtime.Environments;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Runtime;

internal sealed class ScopedRuntime(
    ModuleRuntimeBase root,
    ModuleRuntimeBase parent,
    RuntimeEnvironmentContext environmentContext,
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider)
    : ModuleRuntimeBase(environmentContext, loggerFactory, serviceProvider)
{
    private protected override ModuleRuntimeBase Root => root;

    [NotNull]
    private protected override ModuleRuntimeBase? Parent => parent;
}
