using Cyborg.Core.Aot.Modules.Validation.Models;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal static class CollectionHelpers
{
    public static bool TryConstructEnumerationGuardExpression(PropertyModel property, string accessExpression, [NotNullWhen(true)] out string? conditionExpression, out string valueExpression)
    {
        Debug.Assert(property.Collection is not null);

        (conditionExpression, valueExpression) = property switch
        {
            { IsNullable: true, Collection.MaterializationKind: CollectionMaterializationKind.UseImmutableArray } => (
                $"{accessExpression} is {{ IsDefault: false }}",
                $"{accessExpression}.Value"),
            { IsNullable: true, Symbol.Type.IsValueType: true } => (
                $"{accessExpression} is not null",
                $"{accessExpression}.Value"),
            { IsNullable: true } => (
                $"{accessExpression} is not null",
                accessExpression),
            { Collection.MaterializationKind: CollectionMaterializationKind.UseImmutableArray } => (
                $"!{accessExpression}.IsDefault",
                accessExpression),
            { HasDefault: false, Symbol.Type.IsValueType: false } => (
                $"{accessExpression} is not null",
                accessExpression),
            _ => (null, accessExpression),
        };
        return conditionExpression is not null;
    }
}
