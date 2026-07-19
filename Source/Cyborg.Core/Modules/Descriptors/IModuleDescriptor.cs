using Cyborg.Core.Modules.Descriptors.Builders;

namespace Cyborg.Core.Modules.Descriptors;

// wrapper for source-generated module implementation
public interface IModuleDescriptor
{
    void Describe(IObjectDescriptionBuilder descriptionBuilder);
}
