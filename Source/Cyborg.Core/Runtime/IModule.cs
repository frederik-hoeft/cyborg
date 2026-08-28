using Cyborg.Core.Runtime.Services.ModuleDescriptors;

namespace Cyborg.Core.Runtime;

public interface IModule
{
    string? Name { get; }

    string? Group { get; }

    IModuleDescriptor GetDescriptor();
}
