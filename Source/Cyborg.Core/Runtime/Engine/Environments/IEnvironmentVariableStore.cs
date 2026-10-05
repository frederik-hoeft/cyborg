namespace Cyborg.Core.Runtime.Engine.Environments;

internal interface IEnvironmentVariableStore : IEnumerable<KeyValuePair<string, object?>>
{
    bool TryGetValue(string name, out object? value);

    void SetValue(string name, object? value);

    bool TryRemove(string name);

    /// <summary>
    /// Allocates the next order token for an element appended in this store.
    /// Tokens from one store sort in allocation order. Tokens from transactions that later join sort in fork order.
    /// </summary>
    string AllocateCollectionOrderToken();
}
