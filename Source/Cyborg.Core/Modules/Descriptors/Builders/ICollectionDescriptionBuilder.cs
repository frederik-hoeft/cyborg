using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Descriptors.Builders;

// generator-facing builder for collection items
public interface ICollectionDescriptionBuilder : IDesciptionBuilder
{
    void AddItem<T>(ImmutableArray<string> hints, T item);

    void AddObjectItem(ImmutableArray<string> hints, Action<IObjectDescriptionBuilder> describe);

    void AddCollectionItem(ImmutableArray<string> hints, Action<ICollectionDescriptionBuilder> describe);
}
