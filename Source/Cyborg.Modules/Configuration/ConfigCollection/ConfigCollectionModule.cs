using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using System.Collections.Immutable;

namespace Cyborg.Modules.Configuration.ConfigCollection;

public sealed record ConfigCollectionModule(ImmutableArray<ModuleReference> Sources) : IModule
{
    public static string ModuleId => "cyborg.modules.config.collection.v1";
}