using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Transactions;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Runtime.Engine.Transactions;

/// <summary>Resolves whether a completed invocation publishes workflow-data changes.</summary>
internal static class TransactionFailurePublication
{
    /// <summary>
    /// <see langword="true"/> when the child transaction's workflow writes join the owner.
    /// Success and skip always publish. Failure and cancellation consult the module setting, then the global default.
    /// </summary>
    public static bool PublishWorkflowData(IModule module, ModuleExitStatus status, IServiceProvider? services)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (status is ModuleExitStatus.Success or ModuleExitStatus.Skipped)
        {
            return true;
        }

        return Resolve(module, services) == TransactionOnError.Commit;
    }

    private static TransactionOnError Resolve(IModule module, IServiceProvider? services)
    {
        if (module is ModuleBase { Transaction: { OnError: { } configured } })
        {
            return configured;
        }

        ITransactionOptionsProvider? provider = services?.GetService<ITransactionOptionsProvider>();
        return provider?.OnError ?? TransactionOnError.Commit;
    }
}
