using Cyborg.Core.Modules.Descriptors.Builders;

namespace Cyborg.Core.Modules.Descriptors;

public interface IModuleDescriptor
{
    void Describe(IObjectDescriptionBuilder descriptionBuilder);
}
