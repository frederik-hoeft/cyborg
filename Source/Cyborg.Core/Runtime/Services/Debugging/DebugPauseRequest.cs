using Cyborg.Core.Runtime.Engine;

namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed class DebugPauseRequest(ModuleExecutionId? executionId, long sessionGeneration)
{
    public ModuleExecutionId? ExecutionId { get; } = executionId;

    public long SessionGeneration { get; } = sessionGeneration;

    public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public LinkedListNode<DebugPauseRequest>? QueueNode { get; set; }
}
