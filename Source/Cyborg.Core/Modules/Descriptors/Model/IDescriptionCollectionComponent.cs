using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Descriptors.Model;

public interface IDescriptionCollectionComponent : IDescriptionValueComponent
{
    ImmutableArray<IDescriptionValueComponent> Items { get; }
}
