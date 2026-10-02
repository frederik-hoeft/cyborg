using Cyborg.Core.Runtime.Engine.Transactions.Internal;

namespace Cyborg.Core.Runtime.Engine;

internal sealed class ScopedRuntime
(
    IModuleRuntime root,
    RuntimeEnvironmentContext environmentContext,
    ModuleRuntimeServices operations,
    ActiveTransaction activeTransaction,
    IServiceProvider serviceProvider,
    ModuleInvocationContext invocationContext
) : ModuleRuntimeBase(environmentContext, operations, activeTransaction, serviceProvider, invocationContext)
{
    protected override IModuleRuntime Root => root;
}
