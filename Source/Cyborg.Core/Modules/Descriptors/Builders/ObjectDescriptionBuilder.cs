using Cyborg.Core.Common.Extensions;
using Cyborg.Core.Modules.Descriptors.Model;
using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Descriptors.Builders;

public sealed class ObjectDescriptionBuilder(
    IDescriptionComponentFactory factory) : IObjectDescriptionBuilder
{
    private readonly ImmutableArray<IDescriptionPropertyComponent>.Builder _properties =
        ImmutableArray.CreateBuilder<IDescriptionPropertyComponent>();

    private IDescriptionObjectComponent? _builtComponent;

    public IDescriptionComponent Build() => BuildComponent();

    public void AddProperty<T>(
        string name,
        ImmutableArray<string> hints,
        T value)
    {
        EnsureMutable();
        ValidateName(name);

        IDescriptionValueComponent valueComponent =
            factory.CreateValue(value, hints: []);
        AddPropertyComponent(name, hints.OrEmpty(), valueComponent);
    }

    public void AddObject(
        string name,
        ImmutableArray<string> hints,
        Action<IObjectDescriptionBuilder> describe)
    {
        EnsureMutable();
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(describe);

        ObjectDescriptionBuilder objectBuilder = new(factory);
        describe(objectBuilder);
        AddPropertyComponent(
            name,
            hints.OrEmpty(),
            objectBuilder.BuildComponent());
    }

    public void AddCollection(
        string name,
        ImmutableArray<string> hints,
        Action<ICollectionDescriptionBuilder> describe)
    {
        EnsureMutable();
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(describe);

        CollectionDescriptionBuilder collectionBuilder = new(factory);
        describe(collectionBuilder);
        AddPropertyComponent(
            name,
            hints.OrEmpty(),
            collectionBuilder.BuildComponent());
    }

    internal IDescriptionObjectComponent BuildComponent(
        ImmutableArray<string> hints = default)
        => _builtComponent ??= factory.CreateObject(
            _properties.ToImmutable(),
            hints.OrEmpty())
            ?? throw new InvalidOperationException(
                "The component factory returned a null object component.");

    private void AddPropertyComponent(
        string name,
        ImmutableArray<string> hints,
        IDescriptionValueComponent value)
    {
        ArgumentNullException.ThrowIfNull(value);

        IDescriptionPropertyComponent property =
            factory.CreateProperty(name, value, hints)
            ?? throw new InvalidOperationException(
                "The component factory returned a null property component.");
        _properties.Add(property);
    }

    private void EnsureMutable()
    {
        if (_builtComponent is not null)
        {
            throw new InvalidOperationException(
                "The module description has already been built and can no longer be modified.");
        }
    }

    private static void ValidateName(string name)
        => ArgumentException.ThrowIfNullOrWhiteSpace(name);
}
