using Cyborg.Core.Modules;
using System.Collections.Immutable;

namespace Cyborg.Modules.Configuration.ConfigMap;

public sealed record ConfigMapModule(ImmutableArray<ConfigEntry> Entries) : IModule
{
    public static string ModuleId => "cyborg.modules.config.map.v1";
}

public sealed record ConfigEntry(string Key, string Value);