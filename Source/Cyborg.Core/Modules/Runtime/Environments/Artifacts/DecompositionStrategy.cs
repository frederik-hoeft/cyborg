namespace Cyborg.Core.Modules.Runtime.Environments.Artifacts;

/// <summary>
/// Controls how decomposable values are published into hierarchical stores.
/// </summary>
/// <remarks>
/// All strategies now publish recursive leaf values only. Intermediate composed objects are never
/// retained in the store; structured instances are reconstructed on read via generated
/// <c>Compose</c> methods. Enum members remain for configuration compatibility.
/// </remarks>
public enum DecompositionStrategy
{
    /// <summary>
    /// Publishes only leaf (non-decomposable) values under dotted paths, recursively expanding nested decomposables.
    /// </summary>
    LeavesOnly,
    /// <summary>
    /// Historical strategy that stored nested decomposables as composed values. Now equivalent to <see cref="LeavesOnly"/>.
    /// </summary>
    [Obsolete("Composed intermediate storage has been removed. Shallow is treated as LeavesOnly (recursive leaf publication).")]
    Shallow,
    /// <summary>
    /// Historical strategy that stored the root and intermediate composed objects. Now equivalent to <see cref="LeavesOnly"/>.
    /// Structured values are reconstructed with Compose when needed.
    /// </summary>
    [Obsolete("Composed intermediate storage has been removed. FullHierarchy is treated as LeavesOnly (recursive leaf publication).")]
    FullHierarchy
}
