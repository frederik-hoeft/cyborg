using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation.Models;

internal sealed record ObjectModel
(
    ObjectShape Shape,
    ImmutableArray<PropertyModel> Children
)
{
    public string NonNullableTypeName => Shape.Type.ToDisplayString(KnownSymbolFormats.Nullable);

    public bool HasChildren => !Children.IsDefaultOrEmpty;
}
