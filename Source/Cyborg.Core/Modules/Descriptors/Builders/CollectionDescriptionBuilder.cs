using Cyborg.Core.Common.Extensions;
using Cyborg.Core.Modules.Descriptors.Model;
using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Descriptors.Builders;

public sealed class CollectionDescriptionBuilder(IDescriptionComponentFactory factory) : ICollectionDescriptionBuilder
{
    private readonly ImmutableArray<IDescriptionValueComponent>.Builder _items = ImmutableArray.CreateBuilder<IDescriptionValueComponent>();
    private IDescriptionCollectionComponent? _builtComponent;

    /// <summary>
    /// Finalizes this builder and returns its immutable collection component.
    /// Repeated calls return the same component.
    /// </summary>
    public IDescriptionComponent Build() => BuildComponent();

    public void AddItem<T>(ImmutableArray<string> hints, T item)
    {
        EnsureMutable();

        IDescriptionValueComponent valueComponent = factory.CreateValue(item, hints.OrEmpty())
            ?? throw new InvalidOperationException("The component factory returned a null value component.");
        _items.Add(valueComponent);
    }

    public void AddObjectItem(ImmutableArray<string> hints, Action<IObjectDescriptionBuilder> describe)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(describe);

        ObjectDescriptionBuilder objectBuilder = new(factory);
        describe(objectBuilder);
        IDescriptionObjectComponent objectComponent = objectBuilder.BuildComponent(hints.OrEmpty());
        _items.Add(objectComponent);
    }

    public void AddCollectionItem(ImmutableArray<string> hints, Action<ICollectionDescriptionBuilder> describe)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(describe);

        CollectionDescriptionBuilder collectionBuilder = new(factory);
        describe(collectionBuilder);
        IDescriptionCollectionComponent collectionComponent = collectionBuilder.BuildComponent(hints.OrEmpty());
        _items.Add(collectionComponent);
    }

    internal IDescriptionCollectionComponent BuildComponent(ImmutableArray<string> hints = default)
        => _builtComponent ??= factory.CreateCollection(_items.ToImmutable(), hints.OrEmpty())
            ?? throw new InvalidOperationException("The component factory returned a null collection component.");

    private void EnsureMutable()
    {
        if (_builtComponent is not null)
        {
            throw new InvalidOperationException(
                "The module description collection has already been built and can no longer be modified.");
        }
    }
}
