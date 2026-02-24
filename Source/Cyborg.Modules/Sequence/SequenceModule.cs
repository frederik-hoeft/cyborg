using Cyborg.Core.Modules;
using Cyborg.Modules.Shared.Model;
using System.Collections.Immutable;

namespace Cyborg.Modules.Sequence;

public sealed record SequenceModule(ImmutableArray<ModuleWithEnvironment> Steps) : IModule
{
    public static string ModuleId => "cyborg.modules.sequence.v1";
}