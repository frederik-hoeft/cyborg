using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Services.ModuleDescriptors.Model;

internal interface IDescriptionComponentFactory
{
    IDescriptionPropertyComponent CreateProperty(string name, IDescriptionValueComponent value, ImmutableArray<string> hints);

    IDescriptionValueComponent CreateValue<T>(T value, ImmutableArray<string> hints);

    IDescriptionObjectComponent CreateObject(ImmutableArray<IDescriptionPropertyComponent> properties, ImmutableArray<string> hints);

    IDescriptionCollectionComponent CreateCollection(ImmutableArray<IDescriptionValueComponent> items, ImmutableArray<string> hints);
}
