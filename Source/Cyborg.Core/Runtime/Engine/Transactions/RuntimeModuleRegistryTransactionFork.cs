using Cyborg.Core.Runtime.Engine.Transactions.Collections;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine.Transactions;

internal sealed class RuntimeModuleRegistryTransactionFork(TransactionalDictionaryFork<string, ModuleContext> modules) : ITransactionParticipantFork
{
    public ITransactionParticipantState CreateBranch() => new RuntimeModuleRegistryTransactionState(modules.CreateBranch());

    public bool TryPrepareMerge(
        ITransactionParticipant participant,
        IReadOnlyList<ITransactionParticipantState> contributors,
        ITransactionConflictStrategy conflictStrategy,
        [NotNullWhen(true)] out ITransactionParticipantState? candidate,
        [NotNullWhen(false)] out TransactionConflict? conflict)
    {
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(contributors);
        ArgumentNullException.ThrowIfNull(conflictStrategy);

        TransactionalDictionary<string, ModuleContext>[] moduleContributors = new TransactionalDictionary<string, ModuleContext>[contributors.Count];
        for (int i = 0; i < contributors.Count; i++)
        {
            RuntimeModuleRegistryTransactionState contributor = (RuntimeModuleRegistryTransactionState)contributors[i];
            moduleContributors[i] = contributor.Modules;
        }

        if (!modules.TrySelectChanges(
                participant,
                moduleContributors,
                static name => name,
                conflictStrategy,
                out Dictionary<string, TransactionalDictionaryChange<ModuleContext>>? selectedChanges,
                out conflict))
        {
            candidate = null;
            return false;
        }

        candidate = new RuntimeModuleRegistryTransactionState(modules.PrepareCandidate(selectedChanges));
        conflict = null;
        return true;
    }
}
