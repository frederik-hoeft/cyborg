using Cyborg.Core.Configuration.Serialization;
using System.Text.Json;

namespace Cyborg.Core.Runtime.Configuration;

public interface IModuleLoader
{
    string ModuleId { get; }

    bool TryLoadModule(ref Utf8JsonReader reader, IJsonLoaderContext context, [NotNullWhen(true)] out IModule? module);

    IModuleWorker CreateWorker(IModule module, IServiceProvider serviceProvider);
}

public interface IModuleLoader<TModule> : IModuleLoader where TModule : class, IModule
{
    IModuleWorker CreateWorker(TModule module, IServiceProvider serviceProvider);
}
