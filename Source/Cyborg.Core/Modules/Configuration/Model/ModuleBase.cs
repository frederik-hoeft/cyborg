using Cyborg.Core.Aot.Modules.Validation.Model;

namespace Cyborg.Core.Modules.Configuration.Model;

public abstract record ModuleBase
{
    public virtual string? Name { get; init; }

    [Required]
    [DefaultInstance]
    public ModuleArtifacts Artifacts { get; init; } = null!;
}