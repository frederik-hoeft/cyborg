using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Transactions;
using Cyborg.Core.Modules.Runtime.Transactions.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Modules.Runtime;

internal sealed class RuntimeModuleRegistry : IRuntimeModuleRegistry
{
    private readonly RuntimeModuleRegistryTransactionParticipant _participant = new();

    public ITransactionParticipant Participant => _participant;

    public void ApplySeed(ExecutionTransaction transaction, ModuleRegistrySeed seed)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(seed);
        RuntimeModuleRegistryTransactionState state = transaction.GetParticipantState(_participant);
        state.ApplySeed(seed);
    }

    public void BindExecutionScope(IServiceProvider services, ExecutionTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(transaction);
        IModuleRegistry? registry = services.GetService<IModuleRegistry>();
        if (registry is null)
        {
            return;
        }
        if (registry is not ITransactionBoundModuleRegistry transactionBoundRegistry)
        {
            throw new InvalidOperationException(
                $"Configured module registry '{registry.GetType().FullName}' does not support execution-transaction binding.");
        }
        transactionBoundRegistry.Bind(transaction.GetParticipantState(_participant));
    }
}
