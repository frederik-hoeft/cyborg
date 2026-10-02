using Cyborg.Core.Runtime.Engine.Transactions.Internal;

namespace Cyborg.Core.Runtime.Engine;

internal delegate ValueTask<ModuleInvocationScope> InvocationScopeFactory(ModuleTransaction childTransaction, string moduleId, IModule module, CancellationToken cancellationToken);
