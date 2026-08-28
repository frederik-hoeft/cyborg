using Cyborg.Core.Runtime.Model;
using System.Collections.Frozen;

namespace Cyborg.Core.Runtime.Configuration;

public sealed class DefaultModuleWorkerFactory(IModuleLoaderRegistry moduleLoaderRegistry, IEnumerable<IModuleLoader> moduleLoaders) : IModuleWorkerFactory
{
    private readonly FrozenDictionary<Type, IModuleLoader> _moduleLoadersByType = moduleLoaders.ToFrozenDictionary(ml => ml.GetType());

    public IModuleWorker CreateWorker(ModuleReference moduleReference, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(moduleReference);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        if (!moduleLoaderRegistry.TryGetModuleLoader(moduleReference.ModuleId, out IModuleLoader? moduleLoader))
        {
            throw new InvalidOperationException($"No module loader found for module id '{moduleReference.ModuleId}'.");
        }
        return moduleLoader.CreateWorker(moduleReference.Definition, serviceProvider);
    }

    public IModuleWorker CreateWorker<TModule>(TModule module, string loader, IServiceProvider serviceProvider) where TModule : class, IModule
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(loader);
        ArgumentNullException.ThrowIfNull(serviceProvider);
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

    public IModuleWorker CreateWorker<TModuleLoader, TModule>(TModule module, IServiceProvider serviceProvider)
        where TModuleLoader : IModuleLoader<TModule>
        where TModule : class, IModule
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        if (_moduleLoadersByType.TryGetValue(typeof(TModuleLoader), out IModuleLoader? moduleLoader) && moduleLoader is IModuleLoader<TModule> typedLoader)
        {
            return typedLoader.CreateWorker(module, serviceProvider);
        }
        throw new InvalidOperationException($"No module loader found for module type {typeof(TModule).FullName} with loader type '{typeof(TModuleLoader).FullName}'.");
    }
}
