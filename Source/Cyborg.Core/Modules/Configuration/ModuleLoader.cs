using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Configuration.Serialization;
using System.Text.Json;

namespace Cyborg.Core.Modules.Configuration;

[GeneratorContractRegistration<ModuleLoaderFactoryGeneratorContract>(ModuleLoaderFactoryGeneratorContract.ModuleLoaderT)]
public abstract class ModuleLoader<TModuleWorker, TModule> : IModuleLoader<TModule>
    where TModuleWorker : class, IModuleWorker
    where TModule : class, IModuleDefinition
{
    public virtual string ModuleId => TModule.ModuleId;

    public virtual bool TryLoadModule(ref Utf8JsonReader reader, IJsonLoaderContext context, [NotNullWhen(true)] out IModule? module)
    {
        TModule? loadedModule = JsonSerializer.Deserialize<TModule>(ref reader, context);
        if (loadedModule is not null)
        {
            module = loadedModule;
            return true;
        }
        module = null;
        return false;
    }

    protected abstract TModuleWorker CreateWorker(TModule module, IServiceProvider serviceProvider);

    IModuleWorker IModuleLoader.CreateWorker(IModule module, IServiceProvider serviceProvider)
    {
        if (module is not TModule typedModule)
        {
            throw new InvalidOperationException($"Module loader '{ModuleId}' cannot activate module definition of type '{module.GetType().FullName}'.");
        }
        return CreateWorker(typedModule, serviceProvider);
    }

    IModuleWorker IModuleLoader<TModule>.CreateWorker(TModule module, IServiceProvider serviceProvider) => CreateWorker(module, serviceProvider);
}
