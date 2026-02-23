using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using System.Collections.Immutable;

namespace Cyborg.Modules.Sequence;

public sealed record SequenceModule(ImmutableArray<ModuleReference> Steps) : IModule
{
    public static string ModuleId => "cyborg.modules.sequence.v1";
}