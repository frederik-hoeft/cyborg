namespace Cyborg.Core.Modules.Descriptors.Model;

// base for property metadata (name-value pair)
public interface IDescriptionPropertyComponent : IDescriptionComponent
{
    string Name { get; }

    IDescriptionValueComponent Value { get; }
}
