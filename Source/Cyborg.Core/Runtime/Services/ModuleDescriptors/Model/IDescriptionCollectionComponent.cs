using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Services.ModuleDescriptors.Model;

public interface IDescriptionCollectionComponent : IDescriptionValueComponent
{
    ImmutableArray<IDescriptionValueComponent> Items { get; }
}
