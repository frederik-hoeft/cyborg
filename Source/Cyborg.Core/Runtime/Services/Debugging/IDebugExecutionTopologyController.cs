using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Hooks;

namespace Cyborg.Core.Runtime.Services.Debugging;

internal interface IDebugExecutionTopologyController : IDebugExecutionTopology, IModuleExecutionLifecycleHook
{
    /// <summary>
    /// Returns whether <paramref name="ancestorId"/> is an open strict ancestor of <paramref name="executionId"/>.
    /// A missing or closed execution is not an ancestor, and an execution is not an ancestor of itself.
    /// </summary>
    bool IsOpenAncestor(ModuleExecutionId executionId, ModuleExecutionId ancestorId);

    void EnrichPreparedModule(ModuleExecutionId executionId, IModule module);

    bool MarkPaused(ModuleExecutionId executionId);

    bool MarkCurrent(ModuleExecutionId executionId);

    bool MarkRunning(ModuleExecutionId executionId);
}
