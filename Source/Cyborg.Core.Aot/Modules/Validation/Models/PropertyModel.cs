using Microsoft.CodeAnalysis;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Models;

internal sealed record PropertyModel
(
    IPropertySymbol Symbol,
    string Name,
    string NullableTypeName,
    string NonNullableTypeName,
    bool IsNullable,
    bool IsValidatableType,
    ImmutableArray<PropertyAspect> Aspects,
    ImmutableArray<PropertyModel> Children,
    CollectionModel? Collection
)
{
    private readonly FrozenDictionary<Type, ImmutableArray<PropertyAspect>> _aspectMap = Aspects.Aggregate(
        seed: new Dictionary<Type, ImmutableArray<PropertyAspect>.Builder>(),
        func: static (dict, aspect) =>
        {
            Type aspectType = aspect.GetType();
            if (!dict.TryGetValue(aspectType, out ImmutableArray<PropertyAspect>.Builder aspects))
            {
                aspects = ImmutableArray.CreateBuilder<PropertyAspect>();
                dict.Add(aspectType, aspects);
            }
            aspects.Add(aspect);
            return dict;
        },
        resultSelector: static dict => dict.ToFrozenDictionary(static kvp => kvp.Key, static kvp => kvp.Value.ToImmutable()));

    public bool HasDefault => Aspects.Any(static aspect => aspect.EnsuresDefault);

    public bool HasAspect<TAspect>() where TAspect : PropertyAspect => _aspectMap.ContainsKey(typeof(TAspect));

    public bool TryGetAspect<TAspect>([NotNullWhen(true)] out TAspect? aspect) where TAspect : PropertyAspect
    {
        if (_aspectMap.TryGetValue(typeof(TAspect), out ImmutableArray<PropertyAspect> aspects))
        {
            if (aspects is not [TAspect firstAspect, ..])
            {
                throw new InvalidOperationException($"Contract violation: Expected at least one aspect of type {typeof(TAspect).FullName}, but found none.");
            }
            aspect = firstAspect;
            return true;
        }
        aspect = null;
        return false;
    }

    public bool TryGetAspects<TAspect>([NotNullWhen(true)] out List<TAspect>? aspects) where TAspect : PropertyAspect
    {
        if (_aspectMap.TryGetValue(typeof(TAspect), out ImmutableArray<PropertyAspect> aspectArray))
        {
            // Cast should never fail here
            aspects = [.. aspectArray.Cast<TAspect>()];
            return true;
        }
        aspects = null;
        return false;
    }

    public bool HasValidatableChildren => IsValidatableType && !Children.IsDefaultOrEmpty;

    public bool HasCollectionElementChildren => Collection is not null
        && Collection.IsElementValidatableType
        && !Collection.ElementChildren.IsDefaultOrEmpty;
}
