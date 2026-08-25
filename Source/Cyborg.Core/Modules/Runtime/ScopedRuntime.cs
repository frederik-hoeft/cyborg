using Cyborg.Core.Modules.Runtime.Environments;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Runtime;

internal sealed class ScopedRuntime(
    IModuleRuntime root,
    IModuleRuntime parent,
    RuntimeEnvironmentContext environmentContext,
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider)
    : ModuleRuntimeBase(environmentContext, loggerFactory, serviceProvider)
{
    private protected override IModuleRuntime Root => root;

    [NotNull]
    private protected override IModuleRuntime? Parent => parent;
}
