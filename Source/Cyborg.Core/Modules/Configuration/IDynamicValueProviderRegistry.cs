using Cyborg.Core.Modules.Configuration.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Configuration;

public interface IDynamicValueProviderRegistry
{
    bool TryGetProvider(string typeName, [NotNullWhen(true)] out IDynamicValueProvider? provider);

    IEnumerable<string> GetRegisteredTypeNames();
}
