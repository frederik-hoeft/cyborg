using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Core.Modules;

public abstract record ModuleBase
{
    [IgnoreOverrides]
    public virtual string? Name { get; init; }

    [IgnoreOverrides]
    public virtual string? Group { get; init; }

    [Required]
    [DefaultInstance]
    public ModuleArtifacts Artifacts { get; init; } = null!;
}