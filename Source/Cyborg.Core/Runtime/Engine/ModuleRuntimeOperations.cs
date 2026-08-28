using Cyborg.Core.Runtime.Services.Transactions;

namespace Cyborg.Core.Runtime.Engine;

internal sealed record ModuleRuntimeOperations
(
    IModuleArtifactPublisher ArtifactPublisher,
    IModuleContextExecutor ContextExecutor,
    IModuleExecutionDispatcher ExecutionDispatcher,
    IRuntimeModuleRegistry ModuleRegistry,
    RuntimeTransactionalServices TransactionalServices
);
