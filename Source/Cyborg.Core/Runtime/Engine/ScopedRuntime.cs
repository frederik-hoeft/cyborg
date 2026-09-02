using Cyborg.Core.Runtime.Engine.Transactions.Internal;

namespace Cyborg.Core.Runtime.Engine;

internal sealed class ScopedRuntime
(
    IModuleRuntime root,
    RuntimeEnvironmentContext environmentContext,
    ModuleRuntimeServices operations,
    ModuleTransaction transaction,
    IServiceProvider serviceProvider,
    ModuleInvocationContext invocationContext
) : ModuleRuntimeBase(environmentContext, operations, transaction, serviceProvider, invocationContext)
{
    protected override IModuleRuntime Root => root;
}
