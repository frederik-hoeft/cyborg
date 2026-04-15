using Cyborg.Core.Configuration.Serialization;
using System.Text.Json;

namespace Cyborg.Core.Modules.Configuration;

public interface IModuleLoader
{
    string ModuleId { get; }

    bool TryCreateModule(ref Utf8JsonReader reader, IJsonLoaderContext context, [NotNullWhen(true)] out IModuleWorker? worker);
}

public interface IModuleLoader<TModule> : IModuleLoader where TModule : class, IModule
{
    IModuleWorker CreateWorker(TModule module, IServiceProvider serviceProvider);
}