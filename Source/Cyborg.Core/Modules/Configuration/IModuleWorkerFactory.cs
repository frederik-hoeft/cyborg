namespace Cyborg.Core.Modules.Configuration;

public interface IModuleWorkerFactory
{
    IModuleWorker CreateModule<TModule>(TModule module, string loader) where TModule : class, IModule;

    IModuleWorker CreateModule<TModuleLoader, TModule>(TModule module)
        where TModuleLoader : IModuleLoader<TModule>
        where TModule : class, IModule;
}