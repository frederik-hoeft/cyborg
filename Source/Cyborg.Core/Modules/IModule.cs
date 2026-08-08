using Cyborg.Core.Modules.Descriptors;

namespace Cyborg.Core.Modules;

public interface IModule
{
    string? Name { get; }

    string? Group { get; }

    IModuleDescriptor GetDescriptor();
}
