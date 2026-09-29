namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

/// <summary>Whether a completed invocation contributes its workflow changes when it joins its owner.</summary>
internal enum TransactionPublicationDisposition
{
    Commit,
    Rollback,
}