using Cyborg.Core.Runtime.Engine.Transactions.Internal;

namespace Cyborg.Core.Runtime.Engine;

internal sealed class ScopedRuntime
(
    IModuleRuntime root,
    IModuleRuntime parent,
    RuntimeEnvironmentContext environmentContext,
    ModuleRuntimeOperations operations,
    ExecutionTransaction transaction,
    IServiceProvider serviceProvider
) : ModuleRuntimeBase(environmentContext, operations, transaction, serviceProvider)
{
    protected override IModuleRuntime Root => root;

    [NotNull]
    protected override IModuleRuntime? Parent => parent;
}
