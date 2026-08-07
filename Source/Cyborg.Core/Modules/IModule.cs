using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Modules.Descriptors;

namespace Cyborg.Core.Modules;

[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.IModule)]
public interface IModule
{
    string? Name { get; }

    string? Group { get; }

    static abstract string ModuleId { get; }

    IModuleDescriptor GetDescriptor();
}
