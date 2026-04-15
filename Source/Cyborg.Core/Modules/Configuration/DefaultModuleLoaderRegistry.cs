using System.Collections.Frozen;

namespace Cyborg.Core.Modules.Configuration;

public sealed class DefaultModuleLoaderRegistry(IEnumerable<IModuleLoader> moduleLoaders) : IModuleLoaderRegistry
{
    private readonly FrozenDictionary<string, IModuleLoader> _configurations = moduleLoaders.ToFrozenDictionary(config => config.ModuleId, StringComparer.OrdinalIgnoreCase);

    public bool TryGetModuleLoader(string name, [NotNullWhen(true)] out IModuleLoader? moduleLoader) => _configurations.TryGetValue(name, out moduleLoader);
}