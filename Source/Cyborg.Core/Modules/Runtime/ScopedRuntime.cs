using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Transactions.Core;

namespace Cyborg.Core.Modules.Runtime;

internal sealed class ScopedRuntime(
    IModuleRuntime root,
    IModuleRuntime parent,
    RuntimeEnvironmentContext environmentContext,
    ModuleRuntimeOperations operations,
    ExecutionTransaction transaction,
    IServiceProvider serviceProvider)
    : ModuleRuntimeBase(environmentContext, operations, transaction, serviceProvider)
{
    private protected override IModuleRuntime Root => root;

    [NotNull]
    private protected override IModuleRuntime? Parent => parent;
}
