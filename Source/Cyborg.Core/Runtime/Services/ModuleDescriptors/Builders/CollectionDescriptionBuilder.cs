using Cyborg.Core.Common.Extensions;
using Cyborg.Core.Runtime.Services.ModuleDescriptors.Model;
using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Services.ModuleDescriptors.Builders;

internal sealed class CollectionDescriptionBuilder : ICollectionDescriptionBuilder
{
    private readonly IDescriptionComponentFactory _factory;
    private readonly ImmutableArray<IDescriptionValueComponent>.Builder _items = ImmutableArray.CreateBuilder<IDescriptionValueComponent>();

    private IDescriptionCollectionComponent? _builtComponent;

    internal CollectionDescriptionBuilder(IDescriptionComponentFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    public void AddItem<T>(T item, ImmutableArray<string> hints = default)
    {
        EnsureMutable();
        _items.Add(_factory.CreateValue(item, hints.OrEmpty()));
    }

    public void AddObjectItem(Action<IObjectDescriptionBuilder> describe, ImmutableArray<string> hints = default)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(describe);

        ObjectDescriptionBuilder objectBuilder = new(_factory);
        describe(objectBuilder);
        _items.Add(objectBuilder.BuildComponent(hints.OrEmpty()));
    }

    public void AddCollectionItem(Action<ICollectionDescriptionBuilder> describe, ImmutableArray<string> hints = default)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(describe);

        CollectionDescriptionBuilder collectionBuilder = new(_factory);
        describe(collectionBuilder);
        _items.Add(collectionBuilder.BuildComponent(hints.OrEmpty()));
    }

    internal IDescriptionCollectionComponent BuildComponent(ImmutableArray<string> hints = default) =>
        _builtComponent ??= _factory.CreateCollection(_items.ToImmutable(), hints.OrEmpty());

    private void EnsureMutable()
    {
        if (_builtComponent is not null)
        {
            throw new InvalidOperationException("The module description collection has already been built and can no longer be modified.");
        }
    }
}
