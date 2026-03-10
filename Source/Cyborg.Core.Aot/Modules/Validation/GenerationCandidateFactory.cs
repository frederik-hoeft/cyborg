using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Core.Aot.Modules.Validation.Processors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation;

internal static class GenerationCandidateFactory
{
    public static readonly SymbolDisplayFormat s_fullyQualifiedNullableFormat =
        SymbolDisplayFormat.FullyQualifiedFormat
            .WithMiscellaneousOptions(
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    private static readonly SymbolDisplayFormat s_fullyQualifiedNonNullableFormat =
        SymbolDisplayFormat.FullyQualifiedFormat
            .WithMiscellaneousOptions(
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    public static GenerationCandidate? Create(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        if (!typeSymbol.IsRecord)
        {
            return CreateFailureCandidate(typeSymbol, ValidationGeneratorDiagnostics.TypeMustBeRecord);
        }

        if (!HasPartialDeclaration(typeSymbol))
        {
            return CreateFailureCandidate(typeSymbol, ValidationGeneratorDiagnostics.TypeMustBePartial);
        }

        ImmutableArray<PropertyModel>.Builder properties = ImmutableArray.CreateBuilder<PropertyModel>();
        List<Diagnostic> diagnostics = [];

        foreach (IPropertySymbol property in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.IsStatic || property.IsIndexer || property.IsImplicitlyDeclared)
            {
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(property.ContainingType, typeSymbol))
            {
                continue;
            }

            if (property.SetMethod is null)
            {
                diagnostics.Add(Diagnostic.Create(ValidationGeneratorDiagnostics.PropertyMustBeSettable, property.Locations.FirstOrDefault(), property.Name, typeSymbol.Name));
                continue;
            }

            if (!TryCreatePropertyModel(typeSymbol, property, diagnostics, ImmutableHashSet<INamedTypeSymbol>.Empty.WithComparer(SymbolEqualityComparer.Default), out PropertyModel? propertyModel))
            {
                continue;
            }

            properties.Add(propertyModel!);
        }

        string ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : typeSymbol.ContainingNamespace.ToDisplayString();

        ImmutableArray<ContainingTypeModel> containingTypes = BuildContainingTypes(typeSymbol);
        string fullyQualifiedTypeName = typeSymbol.ToDisplayString(s_fullyQualifiedNullableFormat);
        string hintName = typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            .Replace('<', '_')
            .Replace('>', '_')
            .Replace('.', '_');

        ModuleModel model = new(
            Namespace: ns,
            TypeName: typeSymbol.Name,
            FullyQualifiedTypeName: fullyQualifiedTypeName,
            HintName: hintName + ".ModuleValidation",
            ContainingTypes: containingTypes,
            Properties: properties.ToImmutable());

        return new GenerationCandidate(model.HintName, model, [.. diagnostics]);
    }

    private static GenerationCandidate CreateFailureCandidate(INamedTypeSymbol symbol, DiagnosticDescriptor descriptor)
        => new(
            symbol.ToDisplayString(),
            null,
            [Diagnostic.Create(descriptor, symbol.Locations.FirstOrDefault(), symbol.Name)]);

    private static bool TryCreatePropertyModel(
        INamedTypeSymbol containingType,
        IPropertySymbol property,
        List<Diagnostic> diagnostics,
        ImmutableHashSet<INamedTypeSymbol> traversalPath,
        out PropertyModel? propertyModel)
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

        ITypeSymbol nonNullableType = UnwrapNullableType(property.Type, out bool isNullable);
        bool isValidatableType = false;
        ImmutableArray<PropertyModel> children = [];

        if (nonNullableType is INamedTypeSymbol unwrappedType && IsValidatableType(unwrappedType))
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

                foreach (IPropertySymbol child in unwrappedType.GetMembers().OfType<IPropertySymbol>())
                {
                    if (child.IsStatic || child.IsIndexer || child.IsImplicitlyDeclared)
                    {
                        continue;
                    }

                    if (!SymbolEqualityComparer.Default.Equals(child.ContainingType, unwrappedType))
                    {
                        continue;
                    }

                    if (child.SetMethod is null)
                    {
                        diagnostics.Add(Diagnostic.Create(ValidationGeneratorDiagnostics.UnsupportedNestedPropertyShape, child.Locations.FirstOrDefault(), child.Name, unwrappedType.Name));
                        continue;
                    }

                    if (TryCreatePropertyModel(unwrappedType, child, diagnostics, childPath, out PropertyModel? childModel) && childModel is not null)
                    {
                        childBuilder.Add(childModel);
                    }
                }

                children = childBuilder.ToImmutable();
            }
        }

        propertyModel = new PropertyModel(
            Name: property.Name,
            NullableTypeName: property.Type.ToDisplayString(s_fullyQualifiedNullableFormat),
            NonNullableTypeName: nonNullableType.ToDisplayString(s_fullyQualifiedNonNullableFormat),
            IsNullable: isNullable,
            IsValidatableType: isValidatableType,
            Aspects: aspects.ToImmutable(),
            Children: children);

        return true;
    }

    private static ImmutableArray<ContainingTypeModel> BuildContainingTypes(INamedTypeSymbol typeSymbol)
    {
        Stack<ContainingTypeModel> stack = new();
        INamedTypeSymbol? current = typeSymbol.ContainingType;

        while (current is not null)
        {
            string keyword = current.IsRecord ? "partial record" : "partial class";
            stack.Push(new ContainingTypeModel($"{keyword} {current.Name}"));
            current = current.ContainingType;
        }

        return [.. stack];
    }

    private static bool HasPartialDeclaration(INamedTypeSymbol typeSymbol) => typeSymbol.DeclaringSyntaxReferences
        .Select(static reference => reference.GetSyntax())
        .OfType<TypeDeclarationSyntax>()
        .Any(static declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));

    private static ITypeSymbol UnwrapNullableType(ITypeSymbol typeSymbol, out bool isNullable)
    {
        if (typeSymbol.NullableAnnotation == NullableAnnotation.Annotated)
        {
            isNullable = true;
            return typeSymbol.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        }

        if (typeSymbol is INamedTypeSymbol namedType && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            isNullable = true;
            return namedType.TypeArguments[0];
        }

        isNullable = false;
        return typeSymbol;
    }

    private static bool IsValidatableType(INamedTypeSymbol typeSymbol)
    {
        foreach (AttributeData attribute in typeSymbol.GetAttributes())
        {
            if (attribute.AttributeClass is { } attributeClass 
                && GetFullMetadataName(attributeClass).Equals(typeof(ValidatableAttribute).FullName, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static string GetFullMetadataName(INamedTypeSymbol type)
    {
        INamedTypeSymbol original = type.OriginalDefinition;

        Stack<string> parts = [];
        ISymbol? current = original;

        while (current is not null)
        {
            switch (current)
            {
                case INamespaceSymbol ns when !ns.IsGlobalNamespace:
                    parts.Push(ns.MetadataName);
                    break;
                case INamedTypeSymbol named:
                    parts.Push(named.MetadataName);
                    break;
            }

            current = current.ContainingSymbol;
        }

        return string.Join(".", parts);
    }
}
