using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Configuration;

public interface IModuleWorkerFactory
{
    IModuleWorker CreateWorker(ModuleReference moduleReference, IServiceProvider serviceProvider);

    IModuleWorker CreateWorker<TModule>(TModule module, string loader, IServiceProvider serviceProvider) where TModule : class, IModule;

    IModuleWorker CreateWorker<TModuleLoader, TModule>(TModule module, IServiceProvider serviceProvider)
        where TModuleLoader : IModuleLoader<TModule>
        where TModule : class, IModule;
}
