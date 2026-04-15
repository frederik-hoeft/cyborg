using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules;

[GeneratorContractRegistration<ModuleLoaderFactoryGeneratorContract>(ModuleLoaderFactoryGeneratorContract.ModuleWorkerContextImplementationT)]
public sealed class DefaultWorkerContext<TModule>(TModule module, IServiceProvider serviceProvider) : IWorkerContext<TModule> where TModule : class, IModule<TModule>
{
    public TModule Module { get; } = module;

    public IServiceProvider ServiceProvider { get; } = serviceProvider;
}
