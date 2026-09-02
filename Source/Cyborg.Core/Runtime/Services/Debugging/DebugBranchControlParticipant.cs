using Cyborg.Core.Runtime.Services.Transactions;

namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed class DebugBranchControlParticipant(IDebugSessionState sessionState) : TransactionalServiceParticipant<DebugBranchControlState>
{
    private readonly IDebugSessionState _sessionState = sessionState ?? throw new ArgumentNullException(nameof(sessionState));

    protected override DebugBranchControlState CreateRootState() => new(_sessionState.Generation, isStepping: false);

    protected override TransactionalServiceFork<DebugBranchControlState> CreateFork(DebugBranchControlState ownerState) =>
        new DebugBranchControlFork(ownerState);
}
