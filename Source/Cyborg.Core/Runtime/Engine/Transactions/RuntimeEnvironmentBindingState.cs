using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions.Collections;

namespace Cyborg.Core.Runtime.Engine.Transactions;

internal sealed class RuntimeEnvironmentBindingState(TransactionalDictionary<EnvironmentVariableBinding, object?> values)
{
    public bool TryGetValue(RuntimeEnvironmentId environmentId, string name, out object? value) =>
        values.TryGetValue(new EnvironmentVariableBinding(environmentId, name), out value);

    public void SetValue(RuntimeEnvironmentId environmentId, string name, object? value) =>
        values.Set(new EnvironmentVariableBinding(environmentId, name), value);

    public void SetValues(RuntimeEnvironmentId environmentId, IEnumerable<KeyValuePair<string, object?>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach ((string name, object? value) in values)
        {
            SetValue(environmentId, name, value);
        }
    }

    public bool TryRemove(RuntimeEnvironmentId environmentId, string name) =>
        values.TryRemove(new EnvironmentVariableBinding(environmentId, name));

    public IEnumerable<KeyValuePair<string, object?>> EnumerateValues(RuntimeEnvironmentId environmentId)
    {
        foreach ((EnvironmentVariableBinding binding, object? value) in values)
        {
            if (binding.EnvironmentId == environmentId)
            {
                yield return new KeyValuePair<string, object?>(binding.Name, value);
            }
        }
    }

    public RuntimeEnvironmentBindingFork CreateFork() => new(values);

    internal TransactionalDictionary<EnvironmentVariableBinding, object?> Values => values;
}
