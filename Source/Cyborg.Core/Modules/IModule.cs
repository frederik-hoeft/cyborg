using Cyborg.Core.Modules.Descriptors;

namespace Cyborg.Core.Modules;

public interface IModule : IModuleDescriptor
{
    string? Name { get; }

    string? Group { get; }

    static abstract string ModuleId { get; }
}
