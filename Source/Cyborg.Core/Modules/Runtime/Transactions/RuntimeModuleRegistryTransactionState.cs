using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Transactions.Collections;
using Cyborg.Core.Modules.Runtime.Transactions.Core;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime.Transactions;

internal sealed class RuntimeModuleRegistryTransactionState : ITransactionParticipantState
{
    private readonly TransactionalDictionary<string, ModuleContext> _modules;

    public RuntimeModuleRegistryTransactionState(TransactionalDictionary<string, ModuleContext> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        _modules = modules;
    }

    public bool TryAddModule(string name, ModuleContext module)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(module);
        return _modules.TryAdd(name, module);
    }

    public bool TryGetModule(string name, [NotNullWhen(true)] out ModuleContext? module)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _modules.TryGetValue(name, out module);
    }

    public bool TryRemoveModule(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _modules.TryRemove(name);
    }

    public void ApplySeed(ModuleRegistrySeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        foreach ((string name, ModuleContext module) in seed.Modules)
        {
            _ = _modules.TryAdd(name, module);
        }
    }

    public ITransactionParticipantFork CreateFork() => new RuntimeModuleRegistryTransactionFork(this, new TransactionalDictionaryFork<string, ModuleContext>(_modules));

    internal TransactionalDictionary<string, ModuleContext> Modules => _modules;

    internal static RuntimeModuleRegistryTransactionState Create(ModuleRegistrySeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        return new RuntimeModuleRegistryTransactionState(
            new TransactionalDictionary<string, ModuleContext>(seed.Modules, StringComparer.Ordinal));
    }
}
