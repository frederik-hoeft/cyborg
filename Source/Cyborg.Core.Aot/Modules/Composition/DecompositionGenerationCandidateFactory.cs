using Cyborg.Core.Aot.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Composition;

internal static class DecompositionGenerationCandidateFactory
{
    public static DecompositionGenerationCandidate Create(DecompositionAnnotatedTarget target)
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

        string namingPolicyPropertyName = GetNamedArgument(target.GeneratorAttribute, nameof(GeneratedDecompositionAttribute.NamingPolicy)) ?? "SnakeCaseLower";
        string namingPolicyProviderTypeName = (target.GeneratorAttribute.NamedArguments.FirstOrDefault(kv => kv.Key == nameof(GeneratedDecompositionAttribute.NamingPolicyProvider)).Value.Value as Type)
            ?.GetFullyQualifiedBaseTypeName()
            ?? KnownTypes.JsonNamingPolicy;

        ImmutableArray<IPropertySymbol> decomposableProperties =
        [
            .. typeSymbol.GetMembers().OfType<IPropertySymbol>()
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
                NamingPolicyProviderTypeName: namingPolicyProviderTypeName,
                NamingPolicyPropertyName: namingPolicyPropertyName,
                DecomposableProperties: decomposableProperties),
            diagnostics.ToImmutable());
    }

    private static bool IsPartial(INamedTypeSymbol typeSymbol) =>
        typeSymbol.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .Any(static declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));

    private static string? GetNamedArgument(AttributeData attributeData, string name)
    {
        foreach (KeyValuePair<string, TypedConstant> kvp in attributeData.NamedArguments)
        {
            string key = kvp.Key;
            TypedConstant value = kvp.Value;
            if (key == name && value.Value is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
            {
                return stringValue;
            }
        }

        return null;
    }
}
