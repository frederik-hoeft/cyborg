using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;

namespace Cyborg.Modules.Borg.Prune;

[GeneratedModuleValidation]
public sealed partial record BorgPruneModule
(
    string? GlobArchives,
    [property: Required] BorgPruneKeepRules Keep,
    bool SaveSpace,
    [property: DefaultTimeSpan("00:30:00")] TimeSpan CheckpointInterval
) : BorgModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.borg.prune.v1.4";

    [IgnoreOverrides]
    [Range<int>(Min = 1, Max = int.MaxValue)]
    public int CheckpointIntervalSeconds => (int)CheckpointInterval.TotalSeconds;
}

[Validatable]
public sealed record BorgPruneKeepRules
(
    int Last,
    int Minutely,
    int Hourly,
    int Daily,
    int Weekly,
    int Monthly,
    int Yearly,
    int Weekly13,
    int Monthly3
);
