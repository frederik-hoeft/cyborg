using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Descriptors.Model;

// not strictly necessary, but may hold additional metadata in the future
public interface IDescriptionCollectionComponent : IDescriptionValueComponent
{
    ImmutableArray<IDescriptionValueComponent> Items { get; }
}
