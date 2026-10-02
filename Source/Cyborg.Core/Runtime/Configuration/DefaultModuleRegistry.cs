using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Configuration;

public sealed class DefaultModuleRegistry : IModuleRegistry, ITransactionBoundModuleRegistry
{
    private Func<RuntimeModuleRegistryTransactionState>? _stateAccessor;

    public bool TryAddModule(string name, ModuleContext module) => RequireState().TryAddModule(name, module);

    public bool TryGetModule(string name, [NotNullWhen(true)] out ModuleContext? module) => RequireState().TryGetModule(name, out module);

    public bool TryRemoveModule(string name) => RequireState().TryRemoveModule(name);

    void ITransactionBoundModuleRegistry.Bind(Func<RuntimeModuleRegistryTransactionState> stateAccessor)
    {
        ArgumentNullException.ThrowIfNull(stateAccessor);
        if (_stateAccessor is not null)
        {
            throw new InvalidOperationException("The module registry is already bound to an execution transaction.");
        }
        _stateAccessor = stateAccessor;
    }

    private RuntimeModuleRegistryTransactionState RequireState() =>
        _stateAccessor?.Invoke() ?? throw new InvalidOperationException("The module registry can only be used from a module execution scope.");
}
