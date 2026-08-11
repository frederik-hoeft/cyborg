namespace Cyborg.Core.Modules.Descriptors.Model;

public interface IDescriptionPropertyComponent : IDescriptionComponent
{
    string Name { get; }

    IDescriptionValueComponent Value { get; }
}
