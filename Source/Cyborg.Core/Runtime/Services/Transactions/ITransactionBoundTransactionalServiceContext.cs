using Cyborg.Core.Runtime.Engine.Transactions.Internal;

namespace Cyborg.Core.Runtime.Services.Transactions;

internal interface ITransactionBoundTransactionalServiceContext
{
    void Bind(RuntimeTransactionalServices services, ModuleTransaction transaction);
}
