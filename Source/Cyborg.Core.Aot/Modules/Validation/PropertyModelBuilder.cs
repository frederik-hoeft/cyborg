using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Aspects;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation;

internal sealed class PropertyModelBuilder(GenerationCandidateFactory factory, List<Diagnostic> diagnostics)
{
    private readonly VisibilityContext<INamedTypeSymbol> _visibilityContext = new(factory.Compilation, factory.TypeSymbol);

    private INamedTypeSymbol CandidateType => factory.TypeSymbol;

    private void AddDiagnostic(DiagnosticDescriptor descriptor, Location? location, params object?[]? messageArgs) =>
        diagnostics.Add(Diagnostic.Create(descriptor, location, messageArgs));

    public ImmutableArray<PropertyModel> Build()
    {
        ImmutableArray<PropertyModel>.Builder properties = ImmutableArray.CreateBuilder<PropertyModel>();
        foreach (IPropertySymbol property in CandidateType.EnumerateMostDerivedMembers(_visibilityContext).OfType<IPropertySymbol>())
        {
            if (property.IsStatic || property.IsIndexer)
            {
                continue;
            }

            if (!TryCreatePropertyModel(CandidateType, property, ImmutableHashSet<INamedTypeSymbol>.Empty.WithComparer(SymbolEqualityComparer.Default), out PropertyModel? propertyModel))
            {
                continue;
            }

            properties.Add(propertyModel);
        }

        return properties.ToImmutable();
    }

    private bool TryCreatePropertyModel(INamedTypeSymbol containingType, IPropertySymbol property, ImmutableHashSet<INamedTypeSymbol> traversalPath, [NotNullWhen(true)] out PropertyModel? propertyModel)
    {
        PropertyProcessingContext processingContext = new(factory.Compilation, containingType, property, factory.ContractInfo, diagnostics);
        if (!ValidationProcessorRegistry.TryProcess(in processingContext, out ImmutableArray<IPropertyAspect> aspects))
        {
            propertyModel = null;
            return false;
        }

        bool isNullable = property.Type.TryUnwrapNullableType(out ITypeSymbol nonNullableType);
        ObjectModel? objectModel = TryCreateObjectModel(property.Type, property, traversalPath);
        CollectionModel? collection = TryCreateCollectionModel(containingType, property, traversalPath);

        propertyModel = new PropertyModel(
            Symbol: property,
            Name: property.Name,
            NullableTypeName: property.Type.ToDisplayString(KnownSymbolFormats.Nullable),
            NonNullableTypeName: nonNullableType.ToDisplayString(KnownSymbolFormats.Nullable),
            IsNullable: isNullable,
            Aspects: aspects,
            Object: objectModel,
            Collection: collection);

        if (property.Type.EqualsIgnoreNullability(SpecialType.System_String) && !propertyModel.HasAspect<Processors.UntaggedAspect>())
        {
            AddDiagnostic(
                ValidationGeneratorDiagnostics.PreferTaggedString,
                property.Locations.FirstOrDefault(),
                property.Name,
                containingType.Name);
        }

        return true;
    }

    private CollectionModel? TryCreateCollectionModel(INamedTypeSymbol containingType, IPropertySymbol property, ImmutableHashSet<INamedTypeSymbol> traversalPath)
    {
        if (!CollectionTypeInspector.TryDescribe(factory.Compilation, property.Type, out CollectionShape? shape))
        {
            return null;
        }

        _ = shape.ElementType.TryUnwrapNullableType(out ITypeSymbol nonNullableElementType);
        ObjectModel? elementObject = TryCreateObjectModel(shape.ElementType, property, traversalPath);

        if (elementObject is not null && !shape.SupportsElementRewrite)
        {
            AddDiagnostic(
                ValidationGeneratorDiagnostics.UnsupportedValidatableCollectionShape,
                property.Locations.FirstOrDefault(),
                property.Name,
                containingType.Name,
                property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }

        return new CollectionModel(
            Shape: shape,
            ElementNullableTypeName: shape.ElementType.ToDisplayString(KnownSymbolFormats.Nullable),
            ElementNonNullableTypeName: nonNullableElementType.ToDisplayString(KnownSymbolFormats.Nullable),
            ElementObject: elementObject);
    }

    private ObjectModel? TryCreateObjectModel(ITypeSymbol declaredType, IPropertySymbol sourceProperty, ImmutableHashSet<INamedTypeSymbol> traversalPath)
    {
        if (!ObjectTypeInspector.TryDescribe(declaredType, out ObjectShape? shape))
        {
            return null;
        }

        ImmutableArray<PropertyModel> children = BuildValidatableChildren(shape.Type, sourceProperty, traversalPath);
        return new ObjectModel(shape, children);
    }

    private ImmutableArray<PropertyModel> BuildValidatableChildren(INamedTypeSymbol validatableType, IPropertySymbol sourceProperty, ImmutableHashSet<INamedTypeSymbol> traversalPath)
    {
        if (!validatableType.IsRecord)
        {
            diagnostics.Add(Diagnostic.Create(ValidationGeneratorDiagnostics.UnsupportedValidatableTypeShape, sourceProperty.Locations.FirstOrDefault(), sourceProperty.Name, validatableType.Name));
            return [];
        }

        if (traversalPath.Contains(validatableType))
        {
            diagnostics.Add(Diagnostic.Create(ValidationGeneratorDiagnostics.ValidatableCycleDetected, sourceProperty.Locations.FirstOrDefault(), validatableType.Name));
            return [];
        }

        ImmutableArray<PropertyModel>.Builder childBuilder = ImmutableArray.CreateBuilder<PropertyModel>();
        ImmutableHashSet<INamedTypeSymbol> childPath = traversalPath.Add(validatableType);

        foreach (IPropertySymbol child in validatableType.EnumerateMostDerivedMembers(_visibilityContext).OfType<IPropertySymbol>())
        {
            if (child.IsStatic || child.IsIndexer)
            {
                continue;
            }

            if (child.SetMethod is not { } setter || !_visibilityContext.IsVisible(setter))
            {
                diagnostics.Add(Diagnostic.Create(ValidationGeneratorDiagnostics.UnsupportedNestedPropertyShape, child.Locations.FirstOrDefault(), child.Name, validatableType.Name));
                continue;
            }

            if (TryCreatePropertyModel(validatableType, child, childPath, out PropertyModel? childModel) && childModel is not null)
            {
                childBuilder.Add(childModel);
            }
        }

        return childBuilder.ToImmutable();
    }
}
