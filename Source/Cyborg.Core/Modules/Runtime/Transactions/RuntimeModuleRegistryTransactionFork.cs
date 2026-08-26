using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Transactions.Collections;
using Cyborg.Core.Modules.Runtime.Transactions.Core;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime.Transactions;

internal sealed class RuntimeModuleRegistryTransactionFork : ITransactionParticipantFork
{
    private readonly TransactionalDictionaryFork<string, ModuleContext> _modules;
    private readonly RuntimeModuleRegistryTransactionState _owner;

    public RuntimeModuleRegistryTransactionFork(
        RuntimeModuleRegistryTransactionState owner,
        TransactionalDictionaryFork<string, ModuleContext> modules)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(modules);
        _owner = owner;
        _modules = modules;
    }

    public ITransactionParticipantState CreateBranch() =>
        new RuntimeModuleRegistryTransactionState(_modules.CreateBranch());

    public bool TryPrepareMerge(
        ITransactionParticipant participant,
        IReadOnlyList<ITransactionParticipantState> contributors,
        ITransactionConflictStrategy conflictStrategy,
        [NotNullWhen(true)] out ITransactionParticipantState? candidate,
        out TransactionConflict? conflict)
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

        if (!_modules.TrySelectChanges(
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

        candidate = new RuntimeModuleRegistryTransactionState(_modules.PrepareCandidate(selectedChanges));
        conflict = null;
        return true;
    }
}
