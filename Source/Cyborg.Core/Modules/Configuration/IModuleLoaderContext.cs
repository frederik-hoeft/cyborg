using System.Text.Json;

namespace Cyborg.Core.Modules.Configuration;

public interface IModuleLoaderContext
{
    IServiceProvider ServiceProvider { get; }

    JsonSerializerOptions JsonSerializerOptions { get; }
}
