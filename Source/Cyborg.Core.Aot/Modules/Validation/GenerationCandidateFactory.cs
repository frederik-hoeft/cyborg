using System.Collections.Immutable;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Core.Aot.Modules.Validation.Processors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cyborg.Core.Aot.Modules.Validation;

internal static class GenerationCandidateFactory
{
    public static readonly SymbolDisplayFormat s_fullyQualifiedNullableFormat =
        SymbolDisplayFormat.FullyQualifiedFormat
            .WithMiscellaneousOptions(
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    public static GenerationCandidate? Create(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
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

            if (!TryCreatePropertyModel(typeSymbol, property, diagnostics, out PropertyModel? propertyModel))
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

        propertyModel = new PropertyModel(
            Name: property.Name,
            TypeName: property.Type.ToDisplayString(s_fullyQualifiedNullableFormat),
            Aspects: aspects.ToImmutable());

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
}
