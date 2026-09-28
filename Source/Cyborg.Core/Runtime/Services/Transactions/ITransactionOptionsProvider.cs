namespace Cyborg.Core.Runtime.Services.Transactions;

/// <summary>Supplies the process-wide transaction failure policy used when a module omits <c>Transaction.OnError</c>.</summary>
public interface ITransactionOptionsProvider
{
    /// <summary>Configuration key for <see cref="OnError"/>.</summary>
    public const string ON_ERROR_KEY = "cyborg.core.transactions.on_error";

    /// <summary>Policy applied to failed and canceled invocations that do not set their own.</summary>
    TransactionOnError OnError { get; }
}
