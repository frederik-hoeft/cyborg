using Cyborg.Core;
using Cyborg.Core.Runtime.Services.Transactions;
using Jab;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Cyborg.TestModules.Transactions;

[ServiceProviderModule]
[Singleton<TransactionalServiceParticipant, TransactionalProbeParticipant>]
[Scoped<TransactionalProbeService>]
public interface ITransactionalProbeServices;

public sealed class TransactionalProbeParticipant : TransactionalServiceParticipant<TransactionalProbeState>
{
    protected override TransactionalProbeState CreateRootState() => new();

    protected override TransactionalServiceFork<TransactionalProbeState> CreateFork(TransactionalProbeState ownerState) =>
        new TransactionalProbeFork();
}

public sealed class TransactionalProbeState;

public sealed class TransactionalProbeService(ITransactionalServiceContext context)
{
    private readonly ITransactionalServiceState<TransactionalProbeState> _state =
        context.GetState<TransactionalProbeParticipant, TransactionalProbeState>();

    public bool IsAvailable => _state.Read(static _ => true);
}

public sealed class TransactionalProbeFork : TransactionalServiceFork<TransactionalProbeState>
{
    public override TransactionalProbeState CreateBranch() => new();

    public override bool TryPrepareMerge(
        IReadOnlyList<TransactionalProbeState> contributors,
        ITransactionalServiceConflictResolver conflictResolver,
        [NotNullWhen(true)] out TransactionalProbeState? candidate)
    {
        ArgumentNullException.ThrowIfNull(contributors);
        ArgumentNullException.ThrowIfNull(conflictResolver);
        candidate = new TransactionalProbeState();
        return true;
    }
}

[ServiceProvider]
[Import<ICyborgCoreServices>]
[Import<ITransactionalProbeServices>]
[Singleton<ILoggerFactory>(Instance = nameof(LoggerFactory))]
[Singleton<JsonNamingPolicy>(Instance = nameof(NamingPolicy))]
public sealed partial class TransactionalProbeServiceProvider
{
    public static ILoggerFactory LoggerFactory { get; } = NullLoggerFactory.Instance;

    public static JsonNamingPolicy NamingPolicy { get; } = JsonNamingPolicy.SnakeCaseLower;
}
