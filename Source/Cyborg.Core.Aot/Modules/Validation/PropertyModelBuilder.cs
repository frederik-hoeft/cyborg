using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Aot.Modules.Validation.Attributess;
using Cyborg.Core.Aot.Modules.Validation.Processors;
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
            if (property.SetMethod is not { } setter || !_visibilityContext.IsVisible(setter))
            {
                AddDiagnostic(ValidationGeneratorDiagnostics.PropertyMustBeSettable, property.Locations.FirstOrDefault(), property.Name, CandidateType.Name);
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
        PropertyAttributeProcessingContext processingContext = new(containingType, property, diagnostics);
        ImmutableArray<PropertyValidationAspect>.Builder aspects = ImmutableArray.CreateBuilder<PropertyValidationAspect>();

        foreach (AttributeData attribute in property.GetAttributes())
        {
            if (!ValidationAttributeProcessorRegistry.TryGetProcessor(attribute, out IPropertyAttributeProcessor? processor) || processor is null)
            {
                continue;
            }

            if (!processor.TryProcess(processingContext, attribute, out PropertyValidationAspect? aspect))
            {
                propertyModel = null;
                return false;
            }

            if (aspect is not null)
            {
                aspects.Add(aspect);
            }
        }

        bool isNullable = property.Type.TryUnwrapNullableType(out ITypeSymbol nonNullableType);
        bool isValidatableType = false;
        ImmutableArray<PropertyModel> children = [];

        if (nonNullableType is INamedTypeSymbol unwrappedType && unwrappedType.HasAttribute<ValidatableAttribute>())
        {
            isValidatableType = true;
            if (!unwrappedType.IsRecord)
            {
                diagnostics.Add(Diagnostic.Create(ValidationGeneratorDiagnostics.UnsupportedValidatableTypeShape, property.Locations.FirstOrDefault(), property.Name, unwrappedType.Name));
            }
            else if (traversalPath.Contains(unwrappedType))
            {
                diagnostics.Add(Diagnostic.Create(ValidationGeneratorDiagnostics.ValidatableCycleDetected, property.Locations.FirstOrDefault(), unwrappedType.Name));
            }
            else
            {
                ImmutableArray<PropertyModel>.Builder childBuilder = ImmutableArray.CreateBuilder<PropertyModel>();
                ImmutableHashSet<INamedTypeSymbol> childPath = traversalPath.Add(unwrappedType);

                foreach (IPropertySymbol child in unwrappedType.EnumerateMostDerivedMembers().OfType<IPropertySymbol>())
                {
                    if (child.IsStatic || child.IsIndexer)
                    {
                        continue;
                    }

                    if (child.SetMethod is not { } setter || !_visibilityContext.IsVisible(setter))
                    {
                        diagnostics.Add(Diagnostic.Create(ValidationGeneratorDiagnostics.UnsupportedNestedPropertyShape, child.Locations.FirstOrDefault(), child.Name, unwrappedType.Name));
                        continue;
                    }

                    if (TryCreatePropertyModel(unwrappedType, child, childPath, out PropertyModel? childModel) && childModel is not null)
                    {
                        childBuilder.Add(childModel);
                    }
                }

                children = childBuilder.ToImmutable();
            }
        }

        propertyModel = new PropertyModel(
            Name: property.Name,
            NullableTypeName: property.Type.ToDisplayString(KnownSymbolFormats.Nullable),
            NonNullableTypeName: nonNullableType.ToDisplayString(KnownSymbolFormats.NonNullable),
            IsNullable: isNullable,
            IsValidatableType: isValidatableType,
            Aspects: aspects.ToImmutable(),
            Children: children);

        return true;
    }
}
