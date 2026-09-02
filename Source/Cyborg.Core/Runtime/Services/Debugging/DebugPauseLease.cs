namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed class DebugPauseLease(DebugPauseCoordinator owner, DebugPauseRequest request) : IDisposable
{
    private DebugPauseCoordinator? _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    private readonly DebugPauseRequest _request = request ?? throw new ArgumentNullException(nameof(request));

    public void Dispose()
    {
        DebugPauseCoordinator? owner = Interlocked.Exchange(ref _owner, null);
        owner?.Release(_request);
    }
}
