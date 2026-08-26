namespace Cyborg.Core.Modules.Runtime;

internal sealed class ModuleRuntimeOperations(
    IModuleArtifactPublisher artifactPublisher,
    IModuleContextExecutor contextExecutor,
    IModuleExecutionDispatcher executionDispatcher)
{
    public IModuleArtifactPublisher ArtifactPublisher { get; } = artifactPublisher ?? throw new ArgumentNullException(nameof(artifactPublisher));

    public IModuleContextExecutor ContextExecutor { get; } = contextExecutor ?? throw new ArgumentNullException(nameof(contextExecutor));

    public IModuleExecutionDispatcher ExecutionDispatcher { get; } = executionDispatcher ?? throw new ArgumentNullException(nameof(executionDispatcher));
}
