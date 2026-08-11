using Cyborg.Core.Aot.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Composition;

internal static class DecompositionGenerationCandidateFactory
{
    private static readonly SymbolDisplayFormat s_fullyQualifiedFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included);

    public static DecompositionGenerationCandidate Create(DecompositionAnnotatedTarget target, DecompositionContractInfo? contractInfo)
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
        string namingPolicyProviderTypeName = (target.GeneratorAttribute.NamedArguments
            .FirstOrDefault(kv => kv.Key == nameof(GeneratedDecompositionAttribute.NamingPolicyProvider)).Value.Value as Type)?.RenderGlobal()
            ?? KnownTypes.JsonNamingPolicy;

        ImmutableArray<IPropertySymbol> properties = [.. EnumerateDecomposableProperties(typeSymbol)];
        ImmutableArray<IParameterSymbol> primaryConstructorParameters = GetPrimaryConstructorParameters(typeSymbol);

        ImmutableArray<DecompositionPropertyModel> propertyModels = [.. properties.Select(property =>
        {
            string convertedKeyExpression = $"{namingPolicyProviderTypeName}.{namingPolicyPropertyName}.ConvertName(nameof({property.Name}))";
            ITypeSymbol propertyType = property.Type;
            bool isNullable = propertyType.TryUnwrapNullableType(out ITypeSymbol unwrappedType);
            // Reference types annotated as nullable without being Nullable<T>
            if (!isNullable && propertyType.NullableAnnotation == NullableAnnotation.Annotated)
            {
                isNullable = true;
                unwrappedType = propertyType.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
            }
            else if (!isNullable)
            {
                unwrappedType = propertyType;
            }

            bool isComposable = IsComposableType(unwrappedType, contractInfo);
            string propertyTypeDisplayName = propertyType.ToDisplayString(s_fullyQualifiedFormat);
            string nonNullableTypeDisplayName = unwrappedType.ToDisplayString(s_fullyQualifiedFormat);
            string composableTypeDisplayName = nonNullableTypeDisplayName;
            bool effectiveNullable = isNullable || propertyType.NullableAnnotation == NullableAnnotation.Annotated;

            return new DecompositionPropertyModel(
                Property: property,
                ConvertedKeyExpression: convertedKeyExpression,
                IsComposable: isComposable,
                IsNullable: effectiveNullable,
                PropertyTypeDisplayName: propertyTypeDisplayName,
                NonNullableTypeDisplayName: nonNullableTypeDisplayName,
                ComposableTypeDisplayName: composableTypeDisplayName);
        })];

        string namespaceName = typeSymbol.ContainingNamespace?.IsGlobalNamespace is false
            ? typeSymbol.ContainingNamespace.ToDisplayString()
            : string.Empty;
        string typeKeyword = typeSymbol.IsRecord ? "record" : "class";
        string typeDisplayName = typeSymbol.ToDisplayString(s_fullyQualifiedFormat);

        return new DecompositionGenerationCandidate(
            new DecompositionGenerationModel(
                Namespace: namespaceName,
                TypeSymbol: typeSymbol,
                TypeKeyword: typeKeyword,
                TypeDisplayName: typeDisplayName,
                NamingPolicyProviderTypeName: namingPolicyProviderTypeName,
                NamingPolicyPropertyName: namingPolicyPropertyName,
                DecomposableProperties: propertyModels,
                PrimaryConstructorParameters: primaryConstructorParameters),
            diagnostics.ToImmutable());
    }

    private static ImmutableArray<IParameterSymbol> GetPrimaryConstructorParameters(INamedTypeSymbol typeSymbol)
    {
        // Prefer an explicitly declared instance constructor that is not a record copy constructor.
        IMethodSymbol? constructor = typeSymbol.InstanceConstructors
            .Where(static ctor => !ctor.IsStatic && ctor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
            .Where(ctor => !IsRecordCopyConstructor(ctor, typeSymbol))
            .OrderByDescending(static ctor => ctor.Parameters.Length)
            .FirstOrDefault();

        return constructor?.Parameters ?? [];
    }

    private static bool IsRecordCopyConstructor(IMethodSymbol constructor, INamedTypeSymbol typeSymbol) =>
        typeSymbol.IsRecord
        && constructor.Parameters.Length == 1
        && SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, typeSymbol);

    private static bool IsComposableType(ITypeSymbol type, DecompositionContractInfo? contractInfo)
    {
        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        // Prefer attribute detection so nested types get Compose calls even when the generic interface
        // is only introduced by the generator in the same compilation.
        if (namedType.GetAttributes().Any(static attr =>
                attr.AttributeClass?.ToDisplayString() == typeof(GeneratedDecompositionAttribute).FullName))
        {
            return true;
        }

        foreach (INamedTypeSymbol iface in namedType.AllInterfaces)
        {
            if (iface is { Name: "IDecomposable", TypeArguments.Length: 1 }
                && iface.ContainingNamespace?.ToDisplayString() == "Cyborg.Core.Configuration.Model")
            {
                return true;
            }

            if (contractInfo is not null
                && SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, contractInfo.IDecomposable)
                && iface.TypeArguments.Length == 1)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<IPropertySymbol> EnumerateDecomposableProperties(INamedTypeSymbol typeSymbol)
    {
        HashSet<string> propertyNames = new(StringComparer.Ordinal);
        for (INamedTypeSymbol? currentType = typeSymbol; currentType is not null; currentType = currentType.BaseType)
        {
            foreach (IPropertySymbol property in currentType.GetMembers().OfType<IPropertySymbol>())
            {
                if (!propertyNames.Add(property.Name)
                    || property is not { DeclaredAccessibility: Accessibility.Public, IsStatic: false }
                    || property.GetAttributes().Any(static attr => attr.AttributeClass?.ToDisplayString() == typeof(DecomposeIgnoreAttribute).FullName))
                {
                    continue;
                }
                yield return property;
            }
        }
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
