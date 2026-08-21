using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering.Collections;

internal readonly record struct CollectionShapeRenderer(CollectionShape Shape)
{
    public CollectionValueAccess Access(string accessExpression) => Shape.AccessKind switch
    {
        CollectionAccessKind.Direct => new CollectionValueAccess(null, accessExpression),
        CollectionAccessKind.NullGuard => new CollectionValueAccess($"{accessExpression} is not null", accessExpression),
        CollectionAccessKind.NullableValue => new CollectionValueAccess($"{accessExpression} is not null", $"{accessExpression}.Value"),
        CollectionAccessKind.ImmutableArray => new CollectionValueAccess($"!{accessExpression}.IsDefault", accessExpression),
        CollectionAccessKind.NullableImmutableArray => new CollectionValueAccess($"{accessExpression} is {{ IsDefault: false }}", $"{accessExpression}.Value"),
        _ => throw new InvalidOperationException($"Unsupported collection access kind '{Shape.AccessKind}'."),
    };

    public CollectionValueAccess ElementAccess(string accessExpression) => Shape.ElementAccessKind switch
    {
        CollectionElementAccessKind.Direct => new CollectionValueAccess(null, accessExpression),
        CollectionElementAccessKind.NullGuard => new CollectionValueAccess($"{accessExpression} is not null", accessExpression),
        CollectionElementAccessKind.NullableValue => new CollectionValueAccess($"{accessExpression} is not null", $"{accessExpression}.Value"),
        _ => throw new InvalidOperationException($"Unsupported collection element access kind '{Shape.ElementAccessKind}'."),
    };

    public string CountExpression(string accessExpression) => Shape.CountKind switch
    {
        CollectionCountKind.ArrayLength =>
            $"({accessExpression}).Length",
        CollectionCountKind.ReadOnlyCollection when Shape.CountInterface is { } countInterface =>
            $"(({countInterface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included))})({accessExpression})).Count",
        CollectionCountKind.ReadOnlyCollection =>
            throw new InvalidOperationException("Collection shape declares IReadOnlyCollection count access without a count interface."),
        _ => throw new InvalidOperationException("Collection shape does not support constant-time count access."),
    };
}
