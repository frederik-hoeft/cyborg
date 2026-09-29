using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Services.Transactions;

namespace Cyborg.Core.Runtime.Engine.Transactions;

/// <summary>Applies a module's on-error setting, falling back to the process-wide default.</summary>
internal sealed class DefaultTransactionCompletionPolicy(ITransactionOptionsProvider? options) : ITransactionCompletionPolicy
{
    public TransactionPublicationDisposition Resolve(IModule module, ModuleExitStatus status)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (status is ModuleExitStatus.Success or ModuleExitStatus.Skipped)
        {
            return TransactionPublicationDisposition.Commit;
        }

        TransactionOnError onError = module is ModuleBase { Transaction: { OnError: { } configured } }
            ? configured
            : options?.OnError ?? TransactionOnError.Commit;
        return onError switch
        {
            TransactionOnError.Commit => TransactionPublicationDisposition.Commit,
            TransactionOnError.Rollback => TransactionPublicationDisposition.Rollback,
            _ => throw new ArgumentOutOfRangeException(nameof(onError), onError, "Unsupported transaction failure policy."),
        };
    }
}