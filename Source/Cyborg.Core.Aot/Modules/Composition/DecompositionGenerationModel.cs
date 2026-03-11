using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Composition;

internal sealed record DecompositionGenerationModel(
    string Namespace,
    INamedTypeSymbol TypeSymbol,
    string TypeKeyword,
    INamedTypeSymbol NamingPolicyProviderType,
    string NamingPolicyPropertyName,
    ImmutableArray<IPropertySymbol> DecomposableProperties);
