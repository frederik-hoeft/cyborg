using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Composition;

internal sealed record DecompositionPropertyModel(
    IPropertySymbol Property,
    string ConvertedKeyExpression,
    bool IsComposable,
    bool IsNullable,
    string PropertyTypeDisplayName,
    string NonNullableTypeDisplayName,
    string ComposableTypeDisplayName);

internal sealed record DecompositionGenerationModel(
    string Namespace,
    INamedTypeSymbol TypeSymbol,
    string TypeKeyword,
    string TypeDisplayName,
    string NamingPolicyProviderTypeName,
    string NamingPolicyPropertyName,
    ImmutableArray<DecompositionPropertyModel> DecomposableProperties,
    ImmutableArray<IParameterSymbol> PrimaryConstructorParameters);
