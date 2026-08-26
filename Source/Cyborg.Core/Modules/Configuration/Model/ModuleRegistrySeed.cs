using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Configuration.Model;

internal sealed class ModuleRegistrySeed
{
    public static ModuleRegistrySeed Empty { get; } = new(ImmutableDictionary<string, ModuleContext>.Empty.WithComparers(StringComparer.Ordinal));

    private readonly ImmutableDictionary<string, ModuleContext> _modules;

    internal ModuleRegistrySeed(ImmutableDictionary<string, ModuleContext> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        _modules = modules;
    }

    public int Count => _modules.Count;

    public IEnumerable<KeyValuePair<string, ModuleContext>> Modules => _modules;
}
