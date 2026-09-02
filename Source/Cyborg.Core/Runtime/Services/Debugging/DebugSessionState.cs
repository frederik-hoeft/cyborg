namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed class DebugSessionState : IDebugSessionStateController
{
    private long _generation;

    public long Generation => Interlocked.Read(ref _generation);

    public long Invalidate() => Interlocked.Increment(ref _generation);
}
