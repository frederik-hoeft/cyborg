using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Cyborg.Core.Modules.Configuration;

public interface IModuleLoader
{
    string ModuleId { get; }

    bool TryCreateModule(ref Utf8JsonReader reader, IModuleLoaderContext context, [NotNullWhen(true)] out IModuleWorker? worker);
}
