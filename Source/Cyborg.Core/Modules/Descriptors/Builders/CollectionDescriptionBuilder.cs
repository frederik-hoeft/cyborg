using Cyborg.Core.Common.Extensions;
using Cyborg.Core.Modules.Descriptors.Model;
using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Descriptors.Builders;

internal sealed class CollectionDescriptionBuilder : ICollectionDescriptionBuilder
{
    private readonly IDescriptionComponentFactory _factory;
    private readonly ImmutableArray<IDescriptionValueComponent>.Builder _items =
        ImmutableArray.CreateBuilder<IDescriptionValueComponent>();

    private IDescriptionCollectionComponent? _builtComponent;

    internal CollectionDescriptionBuilder(IDescriptionComponentFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    public void AddItem<T>(ImmutableArray<string> hints, T item)
    {
        EnsureMutable();
        _items.Add(_factory.CreateValue(item, hints.OrEmpty()));
    }

    public void AddObjectItem(
        ImmutableArray<string> hints,
        Action<IObjectDescriptionBuilder> describe)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(describe);

        ObjectDescriptionBuilder objectBuilder = new(_factory);
        describe(objectBuilder);
        _items.Add(objectBuilder.Build(hints.OrEmpty()));
    }

    public void AddCollectionItem(
        ImmutableArray<string> hints,
        Action<ICollectionDescriptionBuilder> describe)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(describe);

        CollectionDescriptionBuilder collectionBuilder = new(_factory);
        describe(collectionBuilder);
        _items.Add(collectionBuilder.BuildComponent(hints.OrEmpty()));
    }

    internal IDescriptionCollectionComponent BuildComponent(
        ImmutableArray<string> hints = default)
        => _builtComponent ??= _factory.CreateCollection(
            _items.ToImmutable(),
            hints.OrEmpty());

    private void EnsureMutable()
    {
        if (_builtComponent is not null)
        {
            throw new InvalidOperationException(
                "The module description collection has already been built and can no longer be modified.");
        }
    }
}
