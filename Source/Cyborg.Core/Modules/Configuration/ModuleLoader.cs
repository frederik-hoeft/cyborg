using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Configuration.Serialization;
using System.Text.Json;

namespace Cyborg.Core.Modules.Configuration;

[GeneratorContractRegistration<ModuleLoaderFactoryGeneratorContract>(ModuleLoaderFactoryGeneratorContract.ModuleLoaderT)]
public abstract class ModuleLoader<TModuleWorker, TModule>(IServiceProvider serviceProvider) : IModuleLoader<TModule>
    where TModuleWorker : class, IModuleWorker
    where TModule : class, IModule
{
    public virtual string ModuleId => TModule.ModuleId;

    public virtual bool TryCreateModule(ref Utf8JsonReader reader, IJsonLoaderContext context, [NotNullWhen(true)] out IModuleWorker? worker)
    {
        TModule? module = JsonSerializer.Deserialize<TModule>(ref reader, context);
        if (module is not null)
        {
            worker = CreateWorker(module, serviceProvider);
            return true;
        }
        worker = null;
        return false;
    }

    protected abstract TModuleWorker CreateWorker(TModule module, IServiceProvider serviceProvider);

    IModuleWorker IModuleLoader<TModule>.CreateWorker(TModule module, IServiceProvider serviceProvider) => CreateWorker(module, serviceProvider);
}