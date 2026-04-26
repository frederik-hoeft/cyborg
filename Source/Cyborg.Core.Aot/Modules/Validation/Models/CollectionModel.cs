using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation.Models;

internal sealed record CollectionModel(
    ITypeSymbol ElementType,
    string ElementNullableTypeName,
    string ElementNonNullableTypeName,
    bool IsElementNullable,
    bool ElementRequiresNullCheck,
    bool IsElementValidatableType,
    CollectionMaterializationKind MaterializationKind,
    string? MaterializationTypeName,
    ImmutableArray<PropertyModel> ElementChildren)
{
    public bool SupportsElementRewrite => MaterializationKind != CollectionMaterializationKind.None;
}
