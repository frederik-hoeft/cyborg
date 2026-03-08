using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using System.Collections.Immutable;

namespace Cyborg.Modules.Configuration.ConfigMap;

public sealed record ConfigMapModule(ImmutableArray<DynamicKeyValuePair> Entries) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.config.map.v1";
}