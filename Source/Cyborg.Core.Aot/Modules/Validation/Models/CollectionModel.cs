using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Models;

internal sealed record CollectionModel
(
    CollectionShape Shape,
    string ElementNullableTypeName,
    string ElementNonNullableTypeName,
    ObjectModel? ElementObject
)
{
    public ITypeSymbol ElementType => Shape.ElementType;

}
