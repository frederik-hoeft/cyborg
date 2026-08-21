using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering.Collections;

internal readonly record struct CollectionShapeRenderer(CollectionShape Shape)
{
    public ValueAccess Access(string accessExpression) => Shape.AccessKind switch
    {
        CollectionAccessKind.Direct => new ValueAccess(null, accessExpression),
        CollectionAccessKind.NullGuard => new ValueAccess($"{accessExpression} is not null", accessExpression),
        CollectionAccessKind.NullableValue => new ValueAccess($"{accessExpression} is not null", $"{accessExpression}.Value"),
        CollectionAccessKind.ImmutableArray => new ValueAccess($"!{accessExpression}.IsDefault", accessExpression),
        CollectionAccessKind.NullableImmutableArray => new ValueAccess($"{accessExpression} is {{ IsDefault: false }}", $"{accessExpression}.Value"),
        _ => throw new InvalidOperationException($"Unsupported collection access kind '{Shape.AccessKind}'."),
    };

    public ValueAccess ElementAccess(string accessExpression) => Shape.ElementAccessKind.Renderer.Access(accessExpression);

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
