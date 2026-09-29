using Cyborg.Core.Configuration;

namespace Cyborg.Core.Runtime.Services.Transactions;

/// <summary>Reads <see cref="ITransactionOptionsProvider.ON_ERROR_KEY"/>, defaulting to <see cref="TransactionOnError.Commit"/>.</summary>
public sealed class DefaultTransactionOptionsProvider(IConfiguration configuration) : ITransactionOptionsProvider
{
    public TransactionOnError OnError => configuration.Get(ITransactionOptionsProvider.ON_ERROR_KEY, TransactionOnError.Commit);
}
