namespace Cyborg.Core.Runtime.Services.ModuleDescriptors.Model;

public interface IDescriptionPropertyComponent : IDescriptionComponent
{
    string Name { get; }

    IDescriptionValueComponent Value { get; }
}
