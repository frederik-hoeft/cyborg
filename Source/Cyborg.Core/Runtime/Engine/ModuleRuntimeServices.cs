using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Services.Transactions;

namespace Cyborg.Core.Runtime.Engine;

internal sealed record ModuleRuntimeServices
(
    IModuleArtifactPublisher ArtifactPublisher,
    IModuleContextRunner ContextRunner,
    IModuleDispatcher Dispatcher,
    IRuntimeModuleRegistry ModuleRegistry,
    RuntimeTransactionalServices Transactional,
    ITransactionCompletionPolicy CompletionPolicy
);
