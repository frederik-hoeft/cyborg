using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Hooks;

namespace Cyborg.Core.Runtime.Services.Debugging;

internal interface IDebugExecutionTopologyController : IDebugExecutionTopology, IModuleExecutionLifecycleHook
{
    void EnrichPreparedModule(ModuleExecutionId executionId, IModule module);

    bool MarkPaused(ModuleExecutionId executionId);

    bool MarkCurrent(ModuleExecutionId executionId);

    bool MarkRunning(ModuleExecutionId executionId);
}
