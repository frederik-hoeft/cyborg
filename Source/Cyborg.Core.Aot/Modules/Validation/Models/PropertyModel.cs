using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation.Models;

internal sealed record PropertyModel(
    IPropertySymbol Symbol,
    string Name,
    string NullableTypeName,
    string NonNullableTypeName,
    bool IsNullable,
    bool IsValidatableType,
    ImmutableArray<PropertyValidationAspect> Aspects,
    ImmutableArray<PropertyModel> Children,
    CollectionModel? Collection)
{
    public bool HasDefault => Aspects.Any(static aspect => aspect.EnsuresDefault);

    public bool HasValidatableChildren => IsValidatableType && !Children.IsDefaultOrEmpty;

    public bool HasCollectionElementChildren => Collection is not null
        && Collection.IsElementValidatableType
        && !Collection.ElementChildren.IsDefaultOrEmpty;
}
