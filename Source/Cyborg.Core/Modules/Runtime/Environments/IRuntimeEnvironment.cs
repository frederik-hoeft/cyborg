using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Artifacts;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Cyborg.Core.Modules.Runtime.Environments;

public interface IRuntimeEnvironment
{
    string Name { get; }

    bool IsTransient { get; }

    void SetVariable<T>(string name, T value);

    bool TryRemoveVariable(string name);

    string Self { get; }

    string Interpolate(string template);

    VariableSyntaxFactory SyntaxFactory { get; }

    bool TryResolveVariable<T>(string name, [NotNullWhen(true)] out T? value);

    /// <summary>
    /// Resolves the specified value for the given module. The module and value expressions are used for override resolution based on corresponding environment variables.
    /// The module expression is used to determine the module for which the variable is being resolved, while the value expression is used to determine the specific variable being resolved.
    /// This allows for more granular control over variable resolution based on the context of the module and the variable being accessed.
    /// </summary>
    /// <typeparam name="TModule">The type of the module for which the variable is being resolved. This is used to determine the context of the variable resolution and can be used to apply overrides based on the module type.</typeparam>
    /// <typeparam name="T">The type of the value being resolved. This is used to ensure type safety during variable resolution and can be used to apply overrides based on the type of the variable being accessed.</typeparam>
    /// <param name="module">The module for which the variable is being resolved. This is used to determine the context of the variable resolution and can be used to apply overrides based on the module type.</param>
    /// <param name="value">The value being resolved. This is used to ensure type safety during variable resolution and can be used to apply overrides based on the type of the variable being accessed.</param>
    /// <param name="moduleExpression">The expression representing the module for which the variable is being resolved. Used to construct the environment variable name for override resolution based on the module context.</param>
    /// <param name="valueExpression">The value expression representing the variable being resolved. Used to construct the environment variable name for override resolution based on the variable context.</param>
    /// <returns>The resolved value of the variable, or null if the variable could not be resolved. The return value is determined based on the module and value expressions, allowing for overrides based on the context of the module and variable being accessed.</returns>
    [return: NotNullIfNotNull(nameof(value))]
    T? Resolve<TModule, T>(TModule module, T? value, [CallerArgumentExpression(nameof(module))] string? moduleExpression = null, [CallerArgumentExpression(nameof(value))] string? valueExpression = null)
        where TModule : class, IModule;

    /// <summary>
    /// Publishes a decomposable object to the specified path for further processing or distribution.
    /// </summary>
    /// <typeparam name="TModule">The type of the module publishing the decomposable object.</typeparam>
    /// <typeparam name="T">The type of the decomposable object to publish. Must be a reference type implementing the <see cref="IDecomposable"/>
    /// interface.</typeparam>
    /// <param name="module">The module publishing the decomposable object. This parameter is used to provide context for the publication mode.</param>
    /// <param name="root">The root path to which the decomposable object will be published. Cannot be null or empty.</param>
    /// <param name="decomposable">The decomposable object to publish. Must not be null and must implement <see cref="IDecomposable"/>.</param>
    void Publish<TModule, T>(TModule module, string root, T decomposable)
        where TModule : ModuleBase, IModule
        where T : class, IDecomposable;

    /// <summary>
    /// Publishes the decomposed values from the specified object into the target environment using the provided
    /// decomposition strategy.
    /// </summary>
    /// <param name="root">The root key or namespace under which the decomposed values will be published.</param>
    /// <param name="decomposable">The object to be decomposed and published. Must implement the IDecomposable interface.</param>
    /// <param name="strategy">The strategy used to control how the object is decomposed and its values are published.</param>
    /// <param name="publishNullValues">Specifies whether null values should be published. Set to <see langword="true"/> to include null values;
    /// otherwise, they will be omitted.</param>
    void Publish(string root, IDecomposable decomposable, DecompositionStrategy strategy, bool publishNullValues);

    string GetEffectiveNamespace<TModule>(TModule module) where TModule : class, IModule;

    string GetEffectiveNamespace(IModuleWorker module);

    string? EffectiveNamespace { get; }

    internal SelfReferenceScope EnterSelfReferenceScope(IModuleWorker module);
}