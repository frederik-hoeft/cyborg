using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Descriptors.Model;

// not strictly necessary, but may hold additional metadata in the future (e.g., type information, etc.)
public interface IDescriptionObjectComponent : IDescriptionValueComponent
{
    ImmutableArray<IDescriptionPropertyComponent> Properties { get; }
}
