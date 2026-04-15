using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;

namespace Cyborg.Core.Modules.Runtime.Environments.Artifacts;

/// <summary>
/// Defines an interface for managing and exposing artifacts produced by a module during execution. Artifacts can be
/// registered with a name or namespace and made available for further processing or inspection.
/// </summary>
/// <remarks>
/// By default, artifacts
/// are published to the namespace of the module that produced them, using the most specific available identifier.
/// If the module has a name such as "my_module", the artifact is published to that namespace. Otherwise, if the
/// module has a group, the artifact is published to the group namespace. If neither is set, the artifact is
/// published under the module's type id, e.g., "cyborg.modules.my_module.v1". Artifacts may also be published to a
/// fully custom path (namespace + name).
/// <para>
/// Artifacts are published to the module's artifact environment, which is specified by the module's configuration.
/// By default, this is the module's execution environment, but it can be configured to be a different environment,
/// such as the global environment or a named environment.
/// </para>
/// </remarks>
public interface IModuleArtifactsBuilder
{
    VariableSyntaxBuilder SyntaxFactory { get; }

    string Namespace { get; }

    /// <summary>
    /// Exposes an artifact under the specified name in the module's default namespace.
    /// </summary>
    /// <remarks>
    /// Artifacts exposed using this method can be accessed by other modules within the same or inherited environments using the specified name, prefixed by the module's namespace.
    /// The artifact can be any object, and it is the responsibility of the module to ensure a stable contract for the artifact.
    /// </remarks>
    /// <param name="path">The unique name used to identify the artifact within the module context. Cannot be null or empty.</param>
    /// <param name="artifact">The artifact object to expose. May be null if representing an absence of value.</param>
    /// <returns>The same <see cref="IModuleArtifactsBuilder"/> instance for fluent chaining.</returns>
    IModuleArtifactsBuilder Expose(string path, object? artifact);

    /// <summary>
    /// Exposes a module artifact under the specified namespace and name, making it available for retrieval or inspection.
    /// </summary>
    /// <param name="ns">The namespace under which the artifact will be exposed. This value is used to logically group related artifacts.</param>
    /// <param name="name">The name of the artifact within the specified namespace. This value uniquely identifies the artifact in its
    /// namespace.</param>
    /// <param name="artifact">The artifact object to expose. May be null if no artifact is available.</param>
    /// <returns>The same <see cref="IModuleArtifactsBuilder"/> instance for fluent chaining.</returns>
    IModuleArtifactsBuilder Expose(string ns, string name, object? artifact);

    /// <summary>
    /// Exposes a decomposable artifact under the module's default namespace.
    /// </summary>
    /// <typeparam name="T">The type of the artifact being exposed, which must implement the IDecomposable interface.</typeparam>
    /// <param name="artifact">The artifact object to expose.</param>
    /// <returns>The same <see cref="IModuleArtifactsBuilder"/> instance for fluent chaining.</returns>
    IModuleArtifactsBuilder Expose<T>(T artifact) where T : class, IDecomposable;

    /// <summary>
    /// Exposes a decomposable artifact under the specified namespace and returns its module artifacts.
    /// </summary>
    /// <typeparam name="T">The type of artifact to expose. Must implement the <see cref="IDecomposable"/> interface.</typeparam>
    /// <param name="path">The namespace under which the artifact will be exposed. This value determines the scope and accessibility of the
    /// artifact.</param>
    /// <param name="artifact">The decomposable artifact to expose. Must not be null and must implement <see cref="IDecomposable"/>.</param>
    /// <returns>The same <see cref="IModuleArtifactsBuilder"/> instance for fluent chaining.</returns>
    IModuleArtifactsBuilder Expose<T>(string path, T artifact) where T : class, IDecomposable;

    internal IEnvironmentLike Build(ModuleExitStatus exitStatus);
}