using Cyborg.Core.Runtime.Engine.Transactions.Collections;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine.Transactions;

internal sealed class RuntimeModuleRegistryTransactionFork(TransactionalDictionaryFork<string, ModuleContext> modules) : ITransactionParticipantFork
{
    public ITransactionParticipantState CreateBranch() => new RuntimeModuleRegistryTransactionState(modules.CreateBranch());

    public bool TryPrepareMerge(
        ITransactionParticipant participant,
        ITransactionParticipantState ownerContinuation,
        IReadOnlyList<ITransactionParticipantState> children,
        ITransactionConflictStrategy conflictStrategy,
        [NotNullWhen(true)] out ITransactionParticipantState? candidate,
        [NotNullWhen(false)] out TransactionConflict? conflict)
    {
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(ownerContinuation);
        ArgumentNullException.ThrowIfNull(children);
        ArgumentNullException.ThrowIfNull(conflictStrategy);

        TransactionalDictionary<string, ModuleContext>[] moduleContributors = new TransactionalDictionary<string, ModuleContext>[children.Count + 1];
        moduleContributors[0] = ((RuntimeModuleRegistryTransactionState)ownerContinuation).Modules;
        for (int i = 0; i < children.Count; i++)
        {
            RuntimeModuleRegistryTransactionState child = (RuntimeModuleRegistryTransactionState)children[i];
            moduleContributors[i + 1] = child.Modules;
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
