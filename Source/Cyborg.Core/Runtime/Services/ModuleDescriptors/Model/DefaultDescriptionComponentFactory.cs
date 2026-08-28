using Cyborg.Core.Common.Extensions;
using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Services.ModuleDescriptors.Model;

internal sealed class DefaultDescriptionComponentFactory : IDescriptionComponentFactory
{
    internal static DefaultDescriptionComponentFactory Instance { get; } = new();

    public IDescriptionPropertyComponent CreateProperty(string name, IDescriptionValueComponent value, ImmutableArray<string> hints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        return new DefaultDescriptionPropertyComponent(name, value, hints.OrEmpty());
    }

    public IDescriptionValueComponent CreateValue<T>(T value, ImmutableArray<string> hints) => new DefaultDescriptionValueComponent<T>(value, hints.OrEmpty());

    public IDescriptionObjectComponent CreateObject(ImmutableArray<IDescriptionPropertyComponent> properties, ImmutableArray<string> hints) =>
        new DefaultDescriptionObjectComponent(properties.OrEmpty(), hints.OrEmpty());

    public IDescriptionCollectionComponent CreateCollection(ImmutableArray<IDescriptionValueComponent> items, ImmutableArray<string> hints) =>
        new DefaultDescriptionCollectionComponent(items.OrEmpty(), hints.OrEmpty());
}
