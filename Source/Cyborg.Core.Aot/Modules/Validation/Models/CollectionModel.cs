using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation.Models;

internal sealed record CollectionModel
(
    CollectionShape Shape,
    string ElementNullableTypeName,
    string ElementNonNullableTypeName,
    bool IsElementValidatableType,
    ImmutableArray<PropertyModel> ElementChildren
)
{
    public ITypeSymbol ElementType => Shape.ElementType;
}
