namespace Cyborg.Core.Runtime.Services.Debugging;

internal interface IDebugSessionStateController : IDebugSessionState
{
    long AdvanceControlCommandSequence();

    long Invalidate();
}
