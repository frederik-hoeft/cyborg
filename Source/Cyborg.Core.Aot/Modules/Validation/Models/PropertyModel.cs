using Cyborg.Core.Aot.Modules.Validation.Aspects;
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
    ImmutableArray<IPropertyAspect> Aspects,
    ObjectModel? Object,
    CollectionModel? Collection
)
{
    private readonly FrozenDictionary<Type, ImmutableArray<IPropertyAspect>> _aspectMap = Aspects.Aggregate(
        seed: new Dictionary<Type, ImmutableArray<IPropertyAspect>.Builder>(),
        func: static (dict, aspect) =>
        {
            Type aspectType = aspect.GetType();
            if (!dict.TryGetValue(aspectType, out ImmutableArray<IPropertyAspect>.Builder aspects))
            {
                aspects = ImmutableArray.CreateBuilder<IPropertyAspect>();
                dict.Add(aspectType, aspects);
            }
            aspects.Add(aspect);
            return dict;
        },
        resultSelector: static dict => dict.ToFrozenDictionary(static kvp => kvp.Key, static kvp => kvp.Value.ToImmutable()));

    public bool HasAspect<TAspect>() where TAspect : class, IPropertyAspect => _aspectMap.ContainsKey(typeof(TAspect));

    public bool TryGetAspect<TAspect>([NotNullWhen(true)] out TAspect? aspect) where TAspect : class, IPropertyAspect
    {
        if (_aspectMap.TryGetValue(typeof(TAspect), out ImmutableArray<IPropertyAspect> aspects))
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

    public bool TryGetAspects<TAspect>([NotNullWhen(true)] out List<TAspect>? aspects) where TAspect : class, IPropertyAspect
    {
        if (_aspectMap.TryGetValue(typeof(TAspect), out ImmutableArray<IPropertyAspect> aspectArray))
        {
            // Cast should never fail here
            aspects = [.. aspectArray.Cast<TAspect>()];
            return true;
        }
        aspects = null;
        return false;
    }

    public bool HasValidatableChildren => Object is { HasChildren: true };

    public bool HasCollectionElementChildren => Collection is { ElementObject.HasChildren: true };

    public bool HasCollectionElementValidationAspects => HasAspect<CollectionElementValidationAspect>();

    public bool HasCollectionValidationWork => Collection is not null
        && (HasCollectionElementChildren || HasCollectionElementValidationAspects);
}
