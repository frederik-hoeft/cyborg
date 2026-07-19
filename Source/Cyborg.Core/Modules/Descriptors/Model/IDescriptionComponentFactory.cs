using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Descriptors.Model;

// factory for creating metadata components, which can be used to build a module description
public interface IDescriptionComponentFactory
{
    // a property is a value with a name and metadata
    IDescriptionPropertyComponent CreateProperty(string name, IDescriptionValueComponent value, ImmutableArray<string> hints);

    // an atomic value (string, number, boolean, DateTime, etc.), can exist standalone or as a property value
    IDescriptionValueComponent CreateValue<T>(T value, ImmutableArray<string> hints);

    // an object is a collection of properties
    IDescriptionObjectComponent CreateObject(ImmutableArray<IDescriptionPropertyComponent> properties, ImmutableArray<string> hints);

    // a collection is a collection of values
    IDescriptionCollectionComponent CreateCollection(ImmutableArray<IDescriptionValueComponent> items, ImmutableArray<string> hints);
}
