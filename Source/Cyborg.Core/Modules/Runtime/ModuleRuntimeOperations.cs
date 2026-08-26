using Cyborg.Core.Modules.Runtime.Transactions.Services;

namespace Cyborg.Core.Modules.Runtime;

internal sealed class ModuleRuntimeOperations(
    IModuleArtifactPublisher artifactPublisher,
    IModuleContextExecutor contextExecutor,
    IModuleExecutionDispatcher executionDispatcher,
    IRuntimeModuleRegistry moduleRegistry,
    RuntimeTransactionalServices transactionalServices)
{
    public IModuleArtifactPublisher ArtifactPublisher { get; } = artifactPublisher ?? throw new ArgumentNullException(nameof(artifactPublisher));

    public IModuleContextExecutor ContextExecutor { get; } = contextExecutor ?? throw new ArgumentNullException(nameof(contextExecutor));

    public IModuleExecutionDispatcher ExecutionDispatcher { get; } = executionDispatcher ?? throw new ArgumentNullException(nameof(executionDispatcher));

    public IRuntimeModuleRegistry ModuleRegistry { get; } = moduleRegistry ?? throw new ArgumentNullException(nameof(moduleRegistry));

    public RuntimeTransactionalServices TransactionalServices { get; } = transactionalServices ?? throw new ArgumentNullException(nameof(transactionalServices));
}
