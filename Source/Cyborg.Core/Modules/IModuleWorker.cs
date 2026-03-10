using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Core.Modules;

[GeneratorContractRegistration<ModuleLoaderFactoryGeneratorContract>(ModuleLoaderFactoryGeneratorContract.IModuleWorker)]
public interface IModuleWorker
{
    string ModuleId { get; }

    internal Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken);
}
