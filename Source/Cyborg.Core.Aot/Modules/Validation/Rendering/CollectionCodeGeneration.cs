using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal static class CollectionCodeGeneration
{
    public static CollectionValueAccess CreateAccess(CollectionShape shape, string accessExpression) =>
        shape.AccessKind switch
        {
            CollectionAccessKind.Direct => new CollectionValueAccess(null, accessExpression),
            CollectionAccessKind.NullGuard => new CollectionValueAccess($"{accessExpression} is not null", accessExpression),
            CollectionAccessKind.NullableValue => new CollectionValueAccess($"{accessExpression} is not null", $"{accessExpression}.Value"),
            CollectionAccessKind.ImmutableArray => new CollectionValueAccess($"!{accessExpression}.IsDefault", accessExpression),
            CollectionAccessKind.NullableImmutableArray => new CollectionValueAccess($"{accessExpression} is {{ IsDefault: false }}", $"{accessExpression}.Value"),
            _ => throw new InvalidOperationException($"Unsupported collection access kind '{shape.AccessKind}'."),
        };

    public static CollectionValueAccess CreateElementAccess(CollectionShape shape, string accessExpression) =>
        shape.ElementAccessKind switch
        {
            CollectionElementAccessKind.Direct => new CollectionValueAccess(null, accessExpression),
            CollectionElementAccessKind.NullGuard => new CollectionValueAccess($"{accessExpression} is not null", accessExpression),
            CollectionElementAccessKind.NullableValue => new CollectionValueAccess($"{accessExpression} is not null", $"{accessExpression}.Value"),
            _ => throw new InvalidOperationException($"Unsupported collection element access kind '{shape.ElementAccessKind}'."),
        };

    public static string CreateCountExpression(CollectionShape shape, string accessExpression)
    {
        return shape.CountKind switch
        {
            CollectionCountKind.ArrayLength => $"({accessExpression}).Length",
            CollectionCountKind.ReadOnlyCollection when shape.CountInterface is { } countInterface =>
                $"(({countInterface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included))})({accessExpression})).Count",
            CollectionCountKind.ReadOnlyCollection =>
                throw new InvalidOperationException("Collection shape declares IReadOnlyCollection count access without a count interface."),
            _ => throw new InvalidOperationException("Collection shape does not support constant-time count access."),
        };
    }

    public static void AppendMaterialization(IndentedStringBuilder builder, CollectionModel collection, string targetVariable, string rewrittenItemsVariable)
    {
        switch (collection.Shape.MaterializationKind)
        {
            case CollectionMaterializationKind.UseList:
                builder.AppendLine($"{targetVariable} = {rewrittenItemsVariable};");
                break;
            case CollectionMaterializationKind.UseArray:
                builder.AppendLine($"{targetVariable} = {KnownTypes.Enumerable}.ToArray({rewrittenItemsVariable});");
                break;
            case CollectionMaterializationKind.UseImmutableArray:
                builder.AppendLine($"{targetVariable} = {KnownTypes.ImmutableArray}.CreateRange({rewrittenItemsVariable});");
                break;
            case CollectionMaterializationKind.ConstructFromList:
                builder.AppendLine($"{targetVariable} = new {RequireMaterializationTypeName(collection)}({rewrittenItemsVariable});");
                break;
            case CollectionMaterializationKind.ParameterlessAdd:
                string safeIdentifier = CreateSafeIdentifier(targetVariable);
                string rewrittenCollectionVariable = $"{safeIdentifier}Collection";
                string rewrittenItemVariable = $"{safeIdentifier}Item";
                string materializationTypeName = RequireMaterializationTypeName(collection);
                // Mutate through the interface variable itself. For value-type ICollection<T> implementations
                // this keeps all Add calls on one boxed instance, which can then be unboxed back into the target.
                builder.AppendBlock(
                    $$"""
                    {{KnownTypes.ICollectionOfT(collection.ElementNullableTypeName)}} {{rewrittenCollectionVariable}} = new {{materializationTypeName}}();
                    foreach ({{collection.ElementNullableTypeName}} {{rewrittenItemVariable}} in {{rewrittenItemsVariable}})
                    {
                        {{rewrittenCollectionVariable}}.Add({{rewrittenItemVariable}});
                    }
                    {{targetVariable}} = ({{materializationTypeName}}){{rewrittenCollectionVariable}};
                    """);
                break;
            default:
                throw new InvalidOperationException("Collection shape does not support element rewrite materialization.");
        }
    }

    private static string RequireMaterializationTypeName(CollectionModel collection) =>
        collection.Shape.MaterializationTypeName
        ?? throw new InvalidOperationException($"Collection materialization kind '{collection.Shape.MaterializationKind}' requires a concrete materialization type.");

    private static string CreateSafeIdentifier(string value) =>
        string.Concat(value.Select(static character => char.IsLetterOrDigit(character) ? character : '_'));
}

internal readonly record struct CollectionValueAccess(string? GuardExpression, string ValueExpression)
{
    public bool RequiresGuard => GuardExpression is not null;

    public string MissingExpression => GuardExpression is { } guard
        ? $"!({guard})"
        : throw new InvalidOperationException("A direct collection access has no missing-value expression.");
}
