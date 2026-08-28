using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Services.ModuleDescriptors.Builders;

public interface ICollectionDescriptionBuilder
{
    void AddItem<T>(T item, ImmutableArray<string> hints = default);

    void AddObjectItem(Action<IObjectDescriptionBuilder> describe, ImmutableArray<string> hints = default);

    void AddCollectionItem(Action<ICollectionDescriptionBuilder> describe, ImmutableArray<string> hints = default);
}
