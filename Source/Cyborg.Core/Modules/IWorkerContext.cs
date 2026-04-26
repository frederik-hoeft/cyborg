using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules;

[GeneratorContractRegistration<ModuleLoaderFactoryGeneratorContract>(ModuleLoaderFactoryGeneratorContract.IModuleWorkerContextT)]
public interface IWorkerContext<TModule> where TModule : class, IModule<TModule>
{
    TModule Module { get; }

    IServiceProvider ServiceProvider { get; }
}
