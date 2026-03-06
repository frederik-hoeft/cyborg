using Cyborg.Core.Modules.Configuration.Serialization;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Configuration;

public sealed class DefaultDynamicValueProviderRegistry(IEnumerable<IDynamicValueProvider> providers) : IDynamicValueProviderRegistry
{
    private readonly FrozenDictionary<string, IDynamicValueProvider> _providers = providers.ToFrozenDictionary(provider => provider.TypeName, StringComparer.OrdinalIgnoreCase);

    public IEnumerable<string> GetRegisteredTypeNames() => _providers.Keys;

    public bool TryGetProvider(string typeName, [NotNullWhen(true)] out IDynamicValueProvider? provider) => _providers.TryGetValue(typeName, out provider);
}
