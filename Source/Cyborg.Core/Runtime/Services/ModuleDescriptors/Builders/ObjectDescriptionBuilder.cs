using Cyborg.Core.Common.Extensions;
using Cyborg.Core.Runtime.Services.ModuleDescriptors.Model;
using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Services.ModuleDescriptors.Builders;

internal sealed class ObjectDescriptionBuilder(IDescriptionComponentFactory factory) : IObjectDescriptionBuilder
{
    private readonly ImmutableArray<IDescriptionPropertyComponent>.Builder _properties = ImmutableArray.CreateBuilder<IDescriptionPropertyComponent>();
    private IDescriptionObjectComponent? _builtComponent;

    public void AddProperty<T>(string name, T value, ImmutableArray<string> hints = default)
    {
        EnsureMutable();
        ValidateName(name);
        // property hints apply to the property itself and the value component
        IDescriptionValueComponent valueComponent = factory.CreateValue(value, hints);
        AddPropertyComponent(name, valueComponent, hints);
    }

    public void AddObject(string name, Action<IObjectDescriptionBuilder> describe, ImmutableArray<string> hints = default)
    {
        EnsureMutable();
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(describe);

        ObjectDescriptionBuilder objectBuilder = new(factory);
        describe(objectBuilder);
        AddPropertyComponent(name, objectBuilder.BuildComponent(), hints);
    }

    public void AddCollection(string name, Action<ICollectionDescriptionBuilder> describe, ImmutableArray<string> hints = default)
    {
        EnsureMutable();
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(describe);

        CollectionDescriptionBuilder collectionBuilder = new(factory);
        describe(collectionBuilder);
        AddPropertyComponent(name, collectionBuilder.BuildComponent(), hints);
    }

    internal IDescriptionObjectComponent BuildComponent(ImmutableArray<string> hints = default) => _builtComponent ??= factory.CreateObject(_properties.ToImmutable(), hints);

    private void AddPropertyComponent(string name, IDescriptionValueComponent value, ImmutableArray<string> hints = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        _properties.Add(factory.CreateProperty(name, value, hints.OrEmpty()));
    }

    private void EnsureMutable()
    {
        if (_builtComponent is not null)
        {
            throw new InvalidOperationException("The module description has already been built and can no longer be modified.");
        }
    }

    private static void ValidateName(string name) => ArgumentException.ThrowIfNullOrWhiteSpace(name);
}
