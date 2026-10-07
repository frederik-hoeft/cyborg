using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Transactions;

namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed class DebugBranchControl : IDebugBranchControl
{
    private readonly IDebugSessionStateController _sessionState;
    private readonly ITransactionalServiceState<DebugBranchControlState> _state;

    public DebugBranchControl(ITransactionalServiceContext context, IDebugSessionState sessionState)
    {
        ArgumentNullException.ThrowIfNull(context);
        _sessionState = sessionState as IDebugSessionStateController
            ?? throw new ArgumentException("The debugger session service must expose controller operations.", nameof(sessionState));
        _state = context.GetState<DebugBranchControlParticipant, DebugBranchControlState>();
    }

    public bool IsStepping
    {
        get
        {
            BranchControlSnapshot snapshot = Read();
            return snapshot.IsStepping && snapshot.SessionGeneration == _sessionState.Generation;
        }
    }

    public ModuleExecutionId? StepOverAnchor
    {
        get
        {
            BranchControlSnapshot snapshot = Read();
            return snapshot.SessionGeneration == _sessionState.Generation ? snapshot.StepOverAnchor : null;
        }
    }

    public void Step() => SetExecutionControl(isStepping: true, stepOverAnchor: null);

    public void Next(ModuleExecutionId anchor) => SetExecutionControl(isStepping: false, stepOverAnchor: anchor);

    public void Continue() => SetExecutionControl(isStepping: false, stepOverAnchor: null);

    private BranchControlSnapshot Read() =>
        _state.Read(static state => new BranchControlSnapshot(state.SessionGeneration, state.IsStepping, state.StepOverAnchor));

    private void SetExecutionControl(bool isStepping, ModuleExecutionId? stepOverAnchor)
    {
        long sessionGeneration = _sessionState.Generation;
        long commandSequence = _sessionState.AdvanceControlCommandSequence();
        _state.Mutate(state =>
        {
            state.SessionGeneration = sessionGeneration;
            state.ControlCommandSequence = commandSequence;
            state.IsStepping = isStepping;
            state.StepOverAnchor = stepOverAnchor;
        });
    }

    private readonly record struct BranchControlSnapshot(long SessionGeneration, bool IsStepping, ModuleExecutionId? StepOverAnchor);
}
