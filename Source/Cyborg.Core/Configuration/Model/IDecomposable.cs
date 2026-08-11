using Cyborg.Core.Aot.Contracts;

namespace Cyborg.Core.Configuration.Model;

/// <summary>
/// Marks a type that can project its public properties into hierarchical key-value leaf entries.
/// </summary>
[GeneratorContractRegistration<ModelDecompositionGeneratorContract>(ModelDecompositionGeneratorContract.IDecomposable)]
public interface IDecomposable
{
    /// <summary>
    /// Projects this instance into hierarchical key-value entries (one per decomposable property).
    /// Nested decomposable values are returned as structured values and are further decomposed by publishers.
    /// </summary>
    IEnumerable<DynamicKeyValuePair> Decompose();
}

/// <summary>
/// Extends <see cref="IDecomposable"/> with a static composition factory that reconstructs
/// <typeparamref name="TSelf"/> from leaf values in an <see cref="IHierarchicalKeyValueStore"/>.
/// </summary>
/// <typeparam name="TSelf">The concrete decomposable type.</typeparam>
public interface IDecomposable<TSelf> : IDecomposable where TSelf : class, IDecomposable<TSelf>
{
    /// <summary>
    /// Reconstructs an instance of <typeparamref name="TSelf"/> from decomposed leaves under <paramref name="rootPath"/>.
    /// </summary>
    /// <param name="store">The hierarchical leaf store to read from.</param>
    /// <param name="rootPath">
    /// The dotted path prefix of this instance in the store (for example <c>host</c> when leaves are
    /// stored as <c>host.hostname</c>, <c>host.port</c>, …).
    /// </param>
    /// <returns>The reconstructed instance.</returns>
    static abstract TSelf Compose(IHierarchicalKeyValueStore store, string rootPath);
}
