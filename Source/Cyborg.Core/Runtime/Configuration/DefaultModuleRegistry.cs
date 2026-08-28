using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Runtime.Engine.Transactions;

namespace Cyborg.Core.Runtime.Configuration;

public sealed class DefaultModuleRegistry : IModuleRegistry, ITransactionBoundModuleRegistry
{
    private RuntimeModuleRegistryTransactionState? _state;

    public bool TryAddModule(string name, ModuleContext module) => RequireState().TryAddModule(name, module);

    public bool TryGetModule(string name, [NotNullWhen(true)] out ModuleContext? module) => RequireState().TryGetModule(name, out module);

    public bool TryRemoveModule(string name) => RequireState().TryRemoveModule(name);

    void ITransactionBoundModuleRegistry.Bind(RuntimeModuleRegistryTransactionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (_state is not null)
        {
            throw new InvalidOperationException("The module registry is already bound to an execution transaction.");
        }
        _state = state;
    }

    private RuntimeModuleRegistryTransactionState RequireState() =>
        _state ?? throw new InvalidOperationException("The module registry can only be used from a module execution scope.");
}
