using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Modules.Configuration.Model;
using System.Runtime.CompilerServices;

namespace Cyborg.Core.Modules.Runtime.Environments;

public interface IRuntimeEnvironment : IEnvironmentLike
{
    string Name { get; }

    bool IsTransient { get; }

    IReadOnlyCollection<string> OverrideResolutionTags { get; }

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
        where TModule : ModuleBase, IModule;

    [return: NotNullIfNotNull(nameof(value))]
    IReadOnlyCollection<T>? ResolveCollection<TModule, T>(TModule module, IReadOnlyCollection<T>? value, [CallerArgumentExpression(nameof(module))] string? moduleExpression = null, [CallerArgumentExpression(nameof(value))] string? valueExpression = null)
        where TModule : ModuleBase, IModule;

    void Publish<TModule, T>(TModule module, string root, T decomposable)
        where TModule : ModuleBase, IModule
        where T : class, IDecomposable;

    string NamespaceOf<TModule>(TModule module) where TModule : ModuleBase, IModule;

    string NamespaceOf(IModuleWorker module);

    void Publish(IEnvironmentLike other);

    internal IRuntimeEnvironment Bind(IModuleWorker module);

    internal IRuntimeEnvironment Bind(string ns);

    internal IRuntimeEnvironment WithOverrideResolutionTags(IReadOnlyCollection<string> tags);

    IEnvironmentLike CreateArtifactCollection(ModuleArtifacts artifacts);

    IEnvironmentLike CreateArtifactCollection();
}
