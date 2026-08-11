using Cyborg.Core.Aot.Contracts;

namespace Cyborg.Core.Configuration;

/// <summary>
/// Hierarchical key-value store of values addressed by dotted path keys.
/// </summary>
/// <remarks>
/// This is the shared abstraction over host configuration and runtime environment variable stores.
/// Decomposition writes leaf values into an implementation of this interface; composition reads those
/// leaves back to reconstruct structured <see cref="Model.IDecomposable"/> instances.
/// Implementations store leaves only — intermediate composed objects are not retained as values.
/// </remarks>
[GeneratorContractRegistration<ModelDecompositionGeneratorContract>(ModelDecompositionGeneratorContract.IHierarchicalKeyValueStore)]
public interface IHierarchicalKeyValueStore : IEnumerable<KeyValuePair<string, object?>>
{
    /// <summary>
    /// Gets the raw value stored at the exact key, or <see langword="null"/> when absent.
    /// </summary>
    /// <remarks>
    /// Does not perform environment indirection or string interpolation. Composition and other
    /// leaf-oriented consumers rely on this raw lookup semantics.
    /// </remarks>
    object? this[string key] { get; }

    /// <summary>
    /// Attempts to read the value stored at the exact key as <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// Does not perform environment indirection or string interpolation. Returns
    /// <see langword="false"/> when the key is absent or the stored value is not of type
    /// <typeparamref name="T"/> (including when a null reference is stored).
    /// </remarks>
    bool TryGetValue<T>(string key, [NotNullWhen(true)] out T? value);
}
