namespace Cyborg.Core.Modules.Runtime.Artifacts;

public enum DecompositionStrategy
{
    /// <summary>
    /// Specifies that only leaf nodes should be published, ignoring any intermediate nodes that have child properties.
    /// </summary>
    LeavesOnly,
    /// <summary>
    /// Decomposes only the top-level properties of the object, without recursively decomposing nested decomposable objects.
    /// </summary>
    Shallow,
    /// <summary>
    /// Specifies that the entire hierarchy of decomposable objects should be published, including all nested decomposable objects and their properties, regardless of their position in the hierarchy.
    /// </summary>
    FullHierarchy
}