using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Text;
using Cyborg.Core.Text.Rendering;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace Cyborg.Core.Runtime.Services.Validation.Internal;

/// <summary>
/// A context object that provides access to the runtime environment and module validation utilities for generated module validation code.
/// </summary>
/// <remarks>
/// This class is intended for internal use by the code generation system and should not be used directly in application code.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.ModuleValidationContext)]
public sealed class ModuleValidationContext
{
    public IModuleRuntime Runtime { get; }

    public IServiceProvider ServiceProvider { get; }

    private ITaggedStringRenderer TaggedStringRenderer => field ??= ServiceProvider.GetRequiredService<ITaggedStringRenderer>();

    private ModuleValidationContext(IModuleRuntime runtime, IServiceProvider serviceProvider)
    {
        Runtime = runtime;
        ServiceProvider = serviceProvider;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ModuleValidationContext Create(IModuleRuntime runtime, IServiceProvider serviceProvider) => new(runtime, serviceProvider);

    public TaggedString Interpolate(string value) => Runtime.Environment.Interpolate(value);

    public TaggedString Interpolate(TaggedString value) => Runtime.Environment.Interpolate(value);

    public TaggedString Interpolate(TaggedString? value) => Runtime.Environment.Interpolate(value);

    /// <summary>Renders a tagged value through the application-configured rendering pipeline.</summary>
    public string Render(TaggedString value) => TaggedStringRenderer.Render(value);

    /// <summary>Renders a nullable tagged value through the application-configured rendering pipeline.</summary>
    public string? Render(TaggedString? value) => value is { } tagged ? Render(tagged) : null;

    [return: NotNullIfNotNull(nameof(value))]
    public string? SelectRawStringOverride<TModule>(TModule module, string? value, string moduleExpression, string valueExpression) where TModule : ModuleBase, IModuleDefinition =>
        Runtime.Environment.SelectRawStringOverride(module, value, moduleExpression, valueExpression);

    public TaggedString SelectRawTaggedStringOverride<TModule>(TModule module, TaggedString value, string moduleExpression, string valueExpression) where TModule : ModuleBase, IModuleDefinition =>
        Runtime.Environment.SelectRawTaggedStringOverride(module, value, moduleExpression, valueExpression);

    [return: NotNullIfNotNull(nameof(value))]
    public TaggedString? SelectRawTaggedStringOverride<TModule>(TModule module, TaggedString? value, string moduleExpression, string valueExpression) where TModule : ModuleBase, IModuleDefinition =>
        Runtime.Environment.SelectRawTaggedStringOverride(module, value, moduleExpression, valueExpression);

    [return: NotNullIfNotNull(nameof(value))]
    public T? ResolveOverride<TModule, T>(TModule module, T? value, string moduleExpression, string valueExpression) where TModule : ModuleBase, IModuleDefinition =>
        Runtime.Environment.Resolve(module, value, moduleExpression, valueExpression);

    [return: NotNullIfNotNull(nameof(value))]
    public IReadOnlyCollection<T>? ResolveCollectionOverride<TModule, T>(TModule module, IReadOnlyCollection<T>? value, string moduleExpression, string valueExpression) where TModule : ModuleBase, IModuleDefinition =>
        Runtime.Environment.ResolveCollection(module, value, moduleExpression, valueExpression);
}
