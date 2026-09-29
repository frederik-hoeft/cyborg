using Cyborg.Core.Runtime.Engine.Transactions.Internal;

namespace Cyborg.Core.Runtime.Engine.Transactions;

/// <summary>Resolves an invocation's transaction publication from its module and execution result.</summary>
internal interface ITransactionCompletionPolicy
{
    TransactionPublicationDisposition Resolve(IModule module, ModuleExitStatus status);
}