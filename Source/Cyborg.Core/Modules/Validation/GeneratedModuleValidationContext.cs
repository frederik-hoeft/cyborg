using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Modules.Runtime.Environments;
using System.ComponentModel;

namespace Cyborg.Core.Modules.Validation;

[EditorBrowsable(EditorBrowsableState.Never)]
[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.GeneratedModuleValidationContext)]
public sealed class GeneratedModuleValidationContext
{
    private readonly IRuntimeEnvironment _environment;

    private GeneratedModuleValidationContext(IRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static GeneratedModuleValidationContext Create(IRuntimeEnvironment environment) => new(environment);

    public string Interpolate(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return _environment.Interpolate(value);
    }

    [return: NotNullIfNotNull(nameof(value))]
    public string? SelectRawStringOverride<TModule>(TModule module, string? value, string moduleExpression, string valueExpression)
        where TModule : ModuleBase, IModule
    {
        RuntimeEnvironment environment = GetRuntimeEnvironment();
        return environment.SelectRawStringOverride(module, value, moduleExpression, valueExpression);
    }

    [return: NotNullIfNotNull(nameof(value))]
    public T? ResolveOverride<TModule, T>(TModule module, T? value, string moduleExpression, string valueExpression)
        where TModule : ModuleBase, IModule =>
        _environment.Resolve(module, value, moduleExpression, valueExpression);

    [return: NotNullIfNotNull(nameof(value))]
    public IReadOnlyCollection<T>? ResolveCollectionOverride<TModule, T>(TModule module, IReadOnlyCollection<T>? value, string moduleExpression, string valueExpression)
        where TModule : ModuleBase, IModule
    {
        RuntimeEnvironment environment = GetRuntimeEnvironment();
        return environment.ResolveCollection(module, value, moduleExpression, valueExpression);
    }

    private RuntimeEnvironment GetRuntimeEnvironment() =>
        _environment as RuntimeEnvironment
        ?? throw new NotSupportedException($"Generated module validation requires an environment derived from {typeof(RuntimeEnvironment).FullName}.");
}
