using Cyborg.Core.Aot.Contracts;

namespace Cyborg.Core.Runtime;

[GeneratorContractRegistration<ModuleLoaderFactoryGeneratorContract>(ModuleLoaderFactoryGeneratorContract.IModuleWorkerContextT)]
public interface IWorkerContext<TModule> where TModule : class, IModule<TModule>
{
    TModule Module { get; }

    IServiceProvider ServiceProvider { get; }
}
