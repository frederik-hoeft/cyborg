using System.Collections.Frozen;

namespace Cyborg.Core.Modules.Configuration;

public sealed class DefaultModuleWorkerFactory(IModuleLoaderRegistry moduleLoaderRegistry, IServiceProvider serviceProvider, IEnumerable<IModuleLoader> moduleLoaders) : IModuleWorkerFactory
{
    private readonly FrozenDictionary<Type, IModuleLoader> _moduleLoadersByType = moduleLoaders.ToFrozenDictionary(ml => ml.GetType());

    public IModuleWorker CreateModule<TModule>(TModule module, string loader) where TModule : class, IModule
    {
        if (moduleLoaderRegistry.TryGetModuleLoader(loader, out IModuleLoader? moduleLoader))
        {
            if (moduleLoader is IModuleLoader<TModule> typedLoader)
            {
                return typedLoader.CreateWorker(module, serviceProvider);
            }
            throw new InvalidOperationException($"Module loader with id '{loader}' does not support module type {typeof(TModule).FullName}.");
        }
        throw new InvalidOperationException($"No module loader found for module type {typeof(TModule).FullName} with loader id '{loader}'.");
    }

    public IModuleWorker CreateModule<TModuleLoader, TModule>(TModule module) 
        where TModuleLoader : IModuleLoader<TModule> 
        where TModule : class, IModule
    {
        if (_moduleLoadersByType.TryGetValue(typeof(TModuleLoader), out IModuleLoader? moduleLoader) && moduleLoader is IModuleLoader<TModule> typedLoader)
        {
            return typedLoader.CreateWorker(module, serviceProvider);
        }
        throw new InvalidOperationException($"No module loader found for module type {typeof(TModule).FullName} with loader type '{typeof(TModuleLoader).FullName}'.");
    }
}