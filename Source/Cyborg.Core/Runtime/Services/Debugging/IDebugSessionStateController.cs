namespace Cyborg.Core.Runtime.Services.Debugging;

internal interface IDebugSessionStateController : IDebugSessionState
{
    long Invalidate();
}
