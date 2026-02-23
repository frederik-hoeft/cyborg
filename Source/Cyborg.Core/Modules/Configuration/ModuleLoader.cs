using Cyborg.Core.Modules.Configuration.Serialization;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Cyborg.Core.Modules.Configuration;

public abstract class ModuleLoader<TModuleWorker, TModule>(IServiceProvider serviceProvider) : IModuleLoader 
    where TModuleWorker : class, IModuleWorker
    where TModule : class, IModule
{
    public virtual string ModuleId => TModule.ModuleId;

    public virtual bool TryCreateModule(ref Utf8JsonReader reader, IModuleLoaderContext context, [NotNullWhen(true)] out IModuleWorker? worker)
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

    protected abstract TModuleWorker CreateWorker(TModule module, IServiceProvider ServiceProvider);
}
