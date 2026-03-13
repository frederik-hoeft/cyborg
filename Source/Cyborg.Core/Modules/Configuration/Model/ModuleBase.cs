using Cyborg.Core.Aot.Modules.Validation.Attributes;

namespace Cyborg.Core.Modules.Configuration.Model;

public abstract record ModuleBase
{
    [IgnoreOverrides]
    public virtual string? Name { get; init; }

    [Required]
    [DefaultInstance]
    public ModuleArtifacts Artifacts { get; init; } = null!;
}