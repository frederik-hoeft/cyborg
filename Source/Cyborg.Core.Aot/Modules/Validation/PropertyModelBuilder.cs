using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation;

internal sealed class PropertyModelBuilder(GenerationCandidateFactory factory, List<Diagnostic> diagnostics)
{
    private readonly VisibilityContext<INamedTypeSymbol> _visibilityContext = new(factory.Context.SemanticModel.Compilation, factory.TypeSymbol);

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
        PropertyProcessingContext processingContext = new(factory.Context.SemanticModel.Compilation, containingType, property, diagnostics);
        if (!ValidationProcessorRegistry.TryProcess(processingContext, out ImmutableArray<PropertyValidationAspect> aspects))
        {
            propertyModel = null;
            return false;
        }

        bool isNullable = property.Type.TryUnwrapNullableType(out ITypeSymbol nonNullableType);
        bool isValidatableType = TryGetValidatableType(nonNullableType, out INamedTypeSymbol? validatableType);
        ImmutableArray<PropertyModel> children = isValidatableType
            ? BuildValidatableChildren(validatableType!, property, traversalPath)
            : [];

        CollectionModel? collection = TryCreateCollectionModel(containingType, property, nonNullableType, traversalPath);

        propertyModel = new PropertyModel(
            Symbol: property,
            Name: property.Name,
            NullableTypeName: property.Type.ToDisplayString(KnownSymbolFormats.Nullable),
            NonNullableTypeName: nonNullableType.ToDisplayString(KnownSymbolFormats.NonNullable),
            IsNullable: isNullable,
            IsValidatableType: isValidatableType,
            Aspects: aspects,
            Children: children,
            Collection: collection);

        return true;
    }

    private CollectionModel? TryCreateCollectionModel(INamedTypeSymbol containingType, IPropertySymbol property, ITypeSymbol nonNullableType, ImmutableHashSet<INamedTypeSymbol> traversalPath)
    {
        if (!CollectionTypeInspector.TryDescribe(factory.Context.SemanticModel.Compilation, nonNullableType, out CollectionTypeInspector.CollectionTypeDescriptor? descriptor) || descriptor is null)
        {
            return null;
        }

        bool isElementNullable = descriptor.ElementType.TryUnwrapNullableType(out ITypeSymbol nonNullableElementType);
        bool isElementValidatableType = TryGetValidatableType(nonNullableElementType, out INamedTypeSymbol? validatableElementType);
        ImmutableArray<PropertyModel> elementChildren = isElementValidatableType
            ? BuildValidatableChildren(validatableElementType!, property, traversalPath)
            : [];

        if (isElementValidatableType && descriptor.MaterializationKind == CollectionMaterializationKind.None)
        {
            AddDiagnostic(
                ValidationGeneratorDiagnostics.UnsupportedValidatableCollectionShape,
                property.Locations.FirstOrDefault(),
                property.Name,
                containingType.Name,
                nonNullableType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }

        return new CollectionModel(
            ElementType: descriptor.ElementType,
            ElementNullableTypeName: descriptor.ElementType.ToDisplayString(KnownSymbolFormats.Nullable),
            ElementNonNullableTypeName: nonNullableElementType.ToDisplayString(KnownSymbolFormats.NonNullable),
            IsElementNullable: isElementNullable,
            ElementRequiresNullCheck: descriptor.ElementType.IsReferenceType || isElementNullable,
            IsElementValidatableType: isElementValidatableType,
            MaterializationKind: descriptor.MaterializationKind,
            MaterializationTypeName: descriptor.MaterializationTypeName,
            ElementChildren: elementChildren);
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

        foreach (IPropertySymbol child in validatableType.EnumerateMostDerivedMembers().OfType<IPropertySymbol>())
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

    private static bool TryGetValidatableType(ITypeSymbol type, [NotNullWhen(true)] out INamedTypeSymbol? validatableType)
    {
        if (type is INamedTypeSymbol namedType && namedType.HasAttribute<ValidatableAttribute>())
        {
            validatableType = namedType;
            return true;
        }

        validatableType = null;
        return false;
    }
}
