using Cyborg.Core.Configuration;
using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments.Artifacts;

namespace Cyborg.Core.Modules.Runtime.Environments;

/// <summary>
/// Mutable hierarchical variable store used by module execution environments.
/// Implements <see cref="IHierarchicalKeyValueStore"/> so generated composition can rebuild
/// structured objects from published leaves.
/// </summary>
public interface IEnvironmentLike : IVariableResolverScope, IHierarchicalKeyValueStore
{
    string Namespace { get; }

    /// <summary>
    /// Publishes a decomposable value as hierarchical leaf variables under <paramref name="root"/>.
    /// Intermediate composed objects are never stored; only leaf values are written.
    /// </summary>
    /// <param name="root">Root path prefix for the published leaves.</param>
    /// <param name="decomposable">The structured value to decompose.</param>
    /// <param name="strategy">
    /// Retained for configuration compatibility. All strategies publish recursive leaves only;
    /// intermediate composed objects are never retained.
    /// </param>
    /// <param name="publishNullValues">When <see langword="true"/>, null leaf values are written; otherwise they are skipped.</param>
    void Publish(string root, IDecomposable decomposable, DecompositionStrategy strategy, bool publishNullValues);

    void SetVariable<T>(string name, T value);

    bool TryRemoveVariable(string name);
}
