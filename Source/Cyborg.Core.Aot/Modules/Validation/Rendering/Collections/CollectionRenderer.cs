using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering.Collections;

internal readonly record struct CollectionRenderer(CollectionModel Model)
{
    public CollectionShapeRenderer Shape => new(Model.Shape);

    public void AppendMaterialization(IndentedStringBuilder builder, string targetVariable, string rewrittenItemsVariable)
    {
        switch (Model.Shape.MaterializationKind)
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
                builder.AppendLine($"{targetVariable} = new {RequireMaterializationTypeName(Model)}({rewrittenItemsVariable});");
                break;
            case CollectionMaterializationKind.ParameterlessAdd:
                string safeIdentifier = CreateSafeIdentifier(targetVariable);
                string rewrittenCollectionVariable = $"{safeIdentifier}Collection";
                string rewrittenItemVariable = $"{safeIdentifier}Item";
                string materializationTypeName = RequireMaterializationTypeName(Model);
                // Mutate through the interface variable itself. For value-type ICollection<T> implementations
                // this keeps all Add calls on one boxed instance, which can then be unboxed back into the target.
                builder.AppendBlock(
                    $$"""
                    {{KnownTypes.ICollectionOfT(Model.ElementNullableTypeName)}} {{rewrittenCollectionVariable}} = new {{materializationTypeName}}();
                    foreach ({{Model.ElementNullableTypeName}} {{rewrittenItemVariable}} in {{rewrittenItemsVariable}})
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
