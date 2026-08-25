using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Transactions.Core;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Runtime;

internal sealed class ScopedRuntime(
    IModuleRuntime root,
    IModuleRuntime parent,
    RuntimeEnvironmentContext environmentContext,
    ILoggerFactory loggerFactory,
    ExecutionTransaction transaction,
    IServiceProvider serviceProvider)
    : ModuleRuntimeBase(environmentContext, loggerFactory, transaction, serviceProvider)
{
    private protected override IModuleRuntime Root => root;

    [NotNull]
    private protected override IModuleRuntime? Parent => parent;
}
