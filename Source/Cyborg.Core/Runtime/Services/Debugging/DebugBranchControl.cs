using Cyborg.Core.Runtime.Services.Transactions;

namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed class DebugBranchControl : IDebugBranchControl
{
    private readonly IDebugSessionState _sessionState;
    private readonly ITransactionalServiceState<DebugBranchControlState> _state;

    public DebugBranchControl(ITransactionalServiceContext context, IDebugSessionState sessionState)
    {
        ArgumentNullException.ThrowIfNull(context);
        _sessionState = sessionState ?? throw new ArgumentNullException(nameof(sessionState));
        _state = context.GetState<DebugBranchControlParticipant, DebugBranchControlState>();
    }

    public bool IsStepping
    {
        get
        {
            BranchControlSnapshot snapshot = _state.Read(static state => new BranchControlSnapshot(state.SessionGeneration, state.IsStepping));
            return snapshot.IsStepping && snapshot.SessionGeneration == _sessionState.Generation;
        }
    }

    public void Step() => SetStepping(isStepping: true);

    public void Continue() => SetStepping(isStepping: false);

    private void SetStepping(bool isStepping)
    {
        long generation = _sessionState.Generation;
        _state.Mutate(state =>
        {
            state.SessionGeneration = generation;
            state.IsStepping = isStepping;
        });
    }

    private readonly record struct BranchControlSnapshot(long SessionGeneration, bool IsStepping);
}
