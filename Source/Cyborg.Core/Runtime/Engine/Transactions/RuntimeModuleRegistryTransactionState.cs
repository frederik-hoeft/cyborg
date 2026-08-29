using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine.Transactions.Collections;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine.Transactions;

internal sealed record RuntimeModuleRegistryTransactionState(TransactionalDictionary<string, ModuleContext> Modules) : ITransactionParticipantState
{
    public bool TryAddModule(string name, ModuleContext module)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(module);
        return Modules.TryAdd(name, module);
    }

    public bool TryGetModule(string name, [NotNullWhen(true)] out ModuleContext? module)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Modules.TryGetValue(name, out module);
    }

    public bool TryRemoveModule(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Modules.TryRemove(name);
    }

    public void ApplySeed(ModuleRegistrySeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        foreach ((string name, ModuleContext module) in seed.Modules)
        {
            _ = Modules.TryAdd(name, module);
        }
    }

    public ITransactionParticipantFork CreateFork() => new RuntimeModuleRegistryTransactionFork(this, new TransactionalDictionaryFork<string, ModuleContext>(Modules));

    internal static RuntimeModuleRegistryTransactionState Create(ModuleRegistrySeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        return new RuntimeModuleRegistryTransactionState(seed.Modules.ToTransactionalDictionary(StringComparer.Ordinal));
    }
}
