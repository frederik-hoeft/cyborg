using Cyborg.Core.Modules.Configuration.Model;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Cyborg.Core.Modules.Configuration.Serialization;

public interface IDynamicValueProvider
{
    string TypeName { get; }

    bool TryCreateValue(ref Utf8JsonReader reader, IModuleLoaderContext context, [NotNullWhen(true)] out DynamicValue? value);
}