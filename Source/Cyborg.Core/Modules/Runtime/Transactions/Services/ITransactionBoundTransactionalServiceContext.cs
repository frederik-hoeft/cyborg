using Cyborg.Core.Modules.Runtime.Transactions.Core;

namespace Cyborg.Core.Modules.Runtime.Transactions.Services;

internal interface ITransactionBoundTransactionalServiceContext
{
    void Bind(RuntimeTransactionalServices services, ExecutionTransaction transaction);
}
