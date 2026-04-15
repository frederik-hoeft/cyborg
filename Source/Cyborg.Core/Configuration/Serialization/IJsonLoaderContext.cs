using System.Text.Json;

namespace Cyborg.Core.Configuration.Serialization;

public interface IJsonLoaderContext
{
    IServiceProvider ServiceProvider { get; }

    JsonSerializerOptions JsonSerializerOptions { get; }
}
