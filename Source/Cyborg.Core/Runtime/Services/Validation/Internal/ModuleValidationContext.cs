using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
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

    private StructuralModulePreparation? _expressions;

    private StructuralModulePreparation Expressions => _expressions ??= new StructuralModulePreparation(
        Runtime.Environment as RuntimeEnvironment ?? throw new InvalidOperationException("Generated module preparation requires a Cyborg runtime environment."));

    private ITaggedStringRenderer TaggedStringRenderer => field ??= ServiceProvider.GetRequiredService<ITaggedStringRenderer>();

    private ModuleValidationContext(IModuleRuntime runtime, IServiceProvider serviceProvider)
    {
        Runtime = runtime;
        ServiceProvider = serviceProvider;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ModuleValidationContext Create(IModuleRuntime runtime, IServiceProvider serviceProvider) => new(runtime, serviceProvider);

    public TaggedString Interpolate(string value, string preparationKey) =>
        Expressions.Interpolate(value, preparationKey);

    public TaggedString Interpolate(TaggedString value, string preparationKey) =>
        Expressions.Interpolate(value, preparationKey);

    public TaggedString Interpolate(TaggedString? value, string preparationKey) =>
        value is { } tagged ? Interpolate(tagged, preparationKey) : default;

    public string? ResolveValueExpression(string value, bool willInterpolate, string preparationKey)
    {
        object? prepared = Expressions.ResolveValueExpression(value, willInterpolate, preparationKey);
        return prepared switch
        {
            string text => text,
            TaggedString tagged => tagged.Value,
            null => null,
            _ => throw new InvalidCastException($"Value expression resolved to '{prepared.GetType().FullName}' instead of string.")
        };
    }

    public TaggedString ResolveValueExpression(TaggedString value, bool willInterpolate, string preparationKey)
    {
        object? prepared = Expressions.ResolveValueExpression(value, willInterpolate, preparationKey);
        return prepared switch
        {
            TaggedString tagged => tagged,
            string text => new TaggedString(text),
            null => throw new InvalidCastException("Value expression resolved to null instead of TaggedString."),
            _ => throw new InvalidCastException($"Value expression resolved to '{prepared.GetType().FullName}' instead of TaggedString.")
        };
    }

    public TaggedString? ResolveValueExpression(TaggedString? value, bool willInterpolate, string preparationKey)
    {
        if (value is not { } tagged)
        {
            return default(TaggedString?);
        }
        object? prepared = Expressions.ResolveValueExpression(tagged, willInterpolate, preparationKey);
        return prepared switch
        {
            TaggedString result => result,
            string text => new TaggedString(text),
            null => default(TaggedString?),
            _ => throw new InvalidCastException($"Value expression resolved to '{prepared.GetType().FullName}' instead of TaggedString.")
        };
    }

    /// <summary>Replaces a selected override with a generated default, discarding its stale structural expression.</summary>
    public T ApplyDefaultReplacement<T>(T replacement, string preparationKey)
    {
        _expressions?.DiscardSelectedOverride(preparationKey);
        return replacement;
    }

    /// <summary>Renders a tagged value through the application-configured rendering pipeline.</summary>
    public string Render(TaggedString value) => TaggedStringRenderer.Render(value);

    /// <summary>Renders a nullable tagged value through the application-configured rendering pipeline.</summary>
    public string? Render(TaggedString? value) => value is { } tagged ? Render(tagged) : null;

    public string? SelectRawStringOverride<TModule>(TModule module, string? value, string moduleExpression, string valueExpression, string preparationKey)
        where TModule : ModuleBase, IModuleDefinition
    {
        if (!Expressions.TrySelectOverride(module, moduleExpression, valueExpression, preparationKey, out object? selected))
        {
            return value;
        }
        return selected switch
        {
            string text => text,
            TaggedString tagged => tagged.Value,
            null => null,
            _ => throw new InvalidCastException($"Selected override is not a string for '{preparationKey}'.")
        };
    }

    public TaggedString SelectRawTaggedStringOverride<TModule>(TModule module, TaggedString value, string moduleExpression, string valueExpression, string preparationKey)
        where TModule : ModuleBase, IModuleDefinition
    {
        if (!Expressions.TrySelectOverride(module, moduleExpression, valueExpression, preparationKey, out object? selected))
        {
            return value;
        }
        return selected switch
        {
            TaggedString tagged => tagged,
            string text => new TaggedString(text),
            _ => throw new InvalidCastException($"Selected override is not textual for '{preparationKey}'.")
        };
    }

    public TaggedString? SelectRawTaggedStringOverride<TModule>(TModule module, TaggedString? value, string moduleExpression, string valueExpression, string preparationKey)
        where TModule : ModuleBase, IModuleDefinition
    {
        if (!Expressions.TrySelectOverride(module, moduleExpression, valueExpression, preparationKey, out object? selected))
        {
            return value;
        }
        return selected switch
        {
            TaggedString tagged => tagged,
            string text => new TaggedString(text),
            null => default(TaggedString?),
            _ => throw new InvalidCastException($"Selected override is not textual for '{preparationKey}'.")
        };
    }

    public T? ResolveOverride<TModule, T>(TModule module, T? value, string moduleExpression, string valueExpression) where TModule : ModuleBase, IModuleDefinition =>
        Runtime.Environment.Resolve(module, value, moduleExpression, valueExpression);

    public IReadOnlyCollection<T>? ResolveCollectionOverride<TModule, T>(
        TModule module,
        IReadOnlyCollection<T>? value,
        string moduleExpression,
        string valueExpression) where TModule : ModuleBase, IModuleDefinition =>
        Runtime.Environment.ResolveCollection(module, value, moduleExpression, valueExpression);
}
