namespace Cyborg.Core.Runtime.Services.Debugging;

/// <summary>
/// Exposes the generation of the current debugger session.
/// </summary>
/// <remarks>
/// A generation change invalidates branch-local debugger control state captured by an earlier session.
/// </remarks>
public interface IDebugSessionState
{
    long Generation { get; }
}
