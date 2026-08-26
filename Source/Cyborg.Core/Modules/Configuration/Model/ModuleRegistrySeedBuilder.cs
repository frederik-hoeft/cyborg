using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Configuration.Model;

internal sealed class ModuleRegistrySeedBuilder
{
    private readonly Dictionary<string, ModuleContext> _modules = new(StringComparer.Ordinal);

    public void Add(string name, ModuleContext module)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(module);
        _ = _modules.TryAdd(name, module);
    }

    public ModuleRegistrySeed Build()
    {
        ImmutableDictionary<string, ModuleContext> modules = _modules.ToImmutableDictionary(StringComparer.Ordinal);
        return modules.Count == 0 ? ModuleRegistrySeed.Empty : new ModuleRegistrySeed(modules);
    }
}
