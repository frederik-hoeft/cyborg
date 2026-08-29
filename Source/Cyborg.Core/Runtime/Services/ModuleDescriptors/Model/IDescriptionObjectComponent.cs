using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Services.ModuleDescriptors.Model;

public interface IDescriptionObjectComponent : IDescriptionValueComponent
{
    ImmutableArray<IDescriptionPropertyComponent> Properties { get; }
}
