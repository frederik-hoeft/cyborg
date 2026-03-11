using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text.Json;

namespace Cyborg.Core.Aot.Modules.Composition;

internal static class DecompositionGenerationCandidateFactory
{
    public static DecompositionGenerationCandidate Create(
        DecompositionAnnotatedTarget target,
        DecompositionContractInfo contractInfo,
        Compilation compilation)
    {
        ImmutableArray<Diagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        INamedTypeSymbol typeSymbol = target.TypeSymbol;

        if (!IsPartial(typeSymbol))
        {
            diagnostics.Add(Diagnostic.Create(
                ModelDecompositionGeneratorDiagnostics.TypeMustBePartial,
                typeSymbol.Locations.FirstOrDefault(),
                typeSymbol.Name));

            return new DecompositionGenerationCandidate(null, diagnostics.ToImmutable());
        }

        string namingPolicyPropertyName = GetNamedArgument(target.GeneratorAttribute, nameof(GeneratedDecompositionAttribute.NamingPolicy))
            ?? nameof(JsonNamingPolicy.SnakeCaseLower);
        INamedTypeSymbol namingPolicyProviderType = GetNamingPolicyProvider(target.GeneratorAttribute, compilation)
            ?? compilation.GetTypeByMetadataName(typeof(JsonNamingPolicy).FullName!)!;

        if (!TryValidateNamingPolicyMember(namingPolicyProviderType, namingPolicyPropertyName, compilation, out Diagnostic? namingPolicyDiagnostic))
        {
            diagnostics.Add(namingPolicyDiagnostic!);
            return new DecompositionGenerationCandidate(null, diagnostics.ToImmutable());
        }

        ImmutableArray<IPropertySymbol> decomposableProperties =
        [
            .. typeSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(static property => property.DeclaredAccessibility is Accessibility.Public)
                .Where(static property => !property.GetAttributes().Any(static attr => attr.AttributeClass?.ToDisplayString() == typeof(DecomposeIgnoreAttribute).FullName))
        ];

        string namespaceName = typeSymbol.ContainingNamespace?.IsGlobalNamespace is false
            ? typeSymbol.ContainingNamespace.ToDisplayString()
            : string.Empty;
        string typeKeyword = typeSymbol.IsRecord ? "record" : "class";

        return new DecompositionGenerationCandidate(
            new DecompositionGenerationModel(
                Namespace: namespaceName,
                TypeSymbol: typeSymbol,
                TypeKeyword: typeKeyword,
                NamingPolicyProviderType: namingPolicyProviderType,
                NamingPolicyPropertyName: namingPolicyPropertyName,
                DecomposableProperties: decomposableProperties),
            diagnostics.ToImmutable());
    }

    private static bool IsPartial(INamedTypeSymbol typeSymbol) =>
        typeSymbol.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax>()
            .Any(static declaration => declaration.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword));

    private static string? GetNamedArgument(AttributeData attributeData, string name)
    {
        foreach ((string key, TypedConstant value) in attributeData.NamedArguments)
        {
            if (key == name && value.Value is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
            {
                return stringValue;
            }
        }

        return null;
    }

    private static INamedTypeSymbol? GetNamingPolicyProvider(AttributeData attributeData, Compilation compilation)
    {
        foreach ((string key, TypedConstant value) in attributeData.NamedArguments)
        {
            if (key == nameof(GeneratedDecompositionAttribute.NamingPolicyProvider) && value.Value is INamedTypeSymbol typeSymbol)
            {
                return typeSymbol;
            }
        }

        return compilation.GetTypeByMetadataName(typeof(JsonNamingPolicy).FullName!);
    }

    private static bool TryValidateNamingPolicyMember(
        INamedTypeSymbol providerType,
        string memberName,
        Compilation compilation,
        out Diagnostic? diagnostic)
    {
        INamedTypeSymbol? jsonNamingPolicyType = compilation.GetTypeByMetadataName(typeof(JsonNamingPolicy).FullName!);
        IPropertySymbol? member = providerType.GetMembers(memberName).OfType<IPropertySymbol>().FirstOrDefault();

        if (member is null)
        {
            diagnostic = Diagnostic.Create(
                ModelDecompositionGeneratorDiagnostics.NamingPolicyPropertyMissing,
                providerType.Locations.FirstOrDefault(),
                providerType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                memberName);
            return false;
        }

        if (!member.IsStatic
            || jsonNamingPolicyType is null
            || !compilation.ClassifyConversion(member.Type, jsonNamingPolicyType).IsImplicit)
        {
            diagnostic = Diagnostic.Create(
                ModelDecompositionGeneratorDiagnostics.NamingPolicyPropertyInvalid,
                member.Locations.FirstOrDefault() ?? providerType.Locations.FirstOrDefault(),
                providerType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                memberName,
                typeof(JsonNamingPolicy).FullName!);
            return false;
        }

        diagnostic = null;
        return true;
    }
}
