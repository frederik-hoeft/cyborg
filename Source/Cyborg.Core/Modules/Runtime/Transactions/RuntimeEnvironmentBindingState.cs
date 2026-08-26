using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Transactions.Collections;

namespace Cyborg.Core.Modules.Runtime.Transactions;

internal sealed class RuntimeEnvironmentBindingState
{
    private readonly TransactionalDictionary<EnvironmentVariableBinding, object?> _values;

    public RuntimeEnvironmentBindingState(TransactionalDictionary<EnvironmentVariableBinding, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = values;
    }

    public bool TryGetValue(RuntimeEnvironmentId environmentId, string name, out object? value) =>
        _values.TryGetValue(new EnvironmentVariableBinding(environmentId, name), out value);

    public void SetValue(RuntimeEnvironmentId environmentId, string name, object? value) =>
        _values.Set(new EnvironmentVariableBinding(environmentId, name), value);

    public void SetValues(RuntimeEnvironmentId environmentId, IEnumerable<KeyValuePair<string, object?>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach ((string name, object? value) in values)
        {
            SetValue(environmentId, name, value);
        }
    }

    public bool TryRemove(RuntimeEnvironmentId environmentId, string name) =>
        _values.TryRemove(new EnvironmentVariableBinding(environmentId, name));

    public IEnumerable<KeyValuePair<string, object?>> EnumerateValues(RuntimeEnvironmentId environmentId)
    {
        foreach ((EnvironmentVariableBinding binding, object? value) in _values)
        {
            if (binding.EnvironmentId == environmentId)
            {
                yield return new KeyValuePair<string, object?>(binding.Name, value);
            }
        }
    }

    public RuntimeEnvironmentBindingFork CreateFork() => new(_values);

    internal TransactionalDictionary<EnvironmentVariableBinding, object?> Values => _values;
}
