using Cyborg.Core.Aot.Contracts;

namespace Cyborg.Core.Runtime;

[GeneratorContractRegistration<ModuleLoaderFactoryGeneratorContract>(ModuleLoaderFactoryGeneratorContract.ModuleWorkerContextImplementationT)]
public sealed class DefaultWorkerContext<TModule>(TModule module, IServiceProvider serviceProvider) : IWorkerContext<TModule> where TModule : class, IModule<TModule>
{
    public TModule Module { get; } = module;

    public IServiceProvider ServiceProvider { get; } = serviceProvider;
}
