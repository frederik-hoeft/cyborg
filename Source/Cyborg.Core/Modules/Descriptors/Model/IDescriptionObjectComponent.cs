using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Descriptors.Model;

public interface IDescriptionObjectComponent : IDescriptionValueComponent
{
    ImmutableArray<IDescriptionPropertyComponent> Properties { get; }
}
