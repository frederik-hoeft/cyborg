using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Models;

/// <summary>
/// Describes the collection semantics that generated code must preserve across every pipeline phase.
/// </summary>
internal sealed record CollectionShape
(
    ITypeSymbol ElementType,
    CollectionAccessKind AccessKind,
    ValueAccessKind ElementAccessKind,
    CollectionCountKind CountKind,
    INamedTypeSymbol? CountInterface,
    CollectionMaterializationKind MaterializationKind,
    string? MaterializationTypeName
)
{
    public bool SupportsCount => CountKind != CollectionCountKind.None;

    public bool SupportsElementRewrite => MaterializationKind != CollectionMaterializationKind.None;
}

/// <summary>
/// Describes how generated code obtains a usable collection value without changing absence/default semantics.
/// </summary>
internal enum CollectionAccessKind
{
    Direct = 0,
    NullGuard,
    NullableValue,
    ImmutableArray,
    NullableImmutableArray,
}

internal enum CollectionCountKind
{
    None = 0,
    ArrayLength,
    ReadOnlyCollection,
}
