using Cyborg.Core.Aot.Modules.Validation.Model;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Core.Modules;

public abstract record ModuleBase
{
    public string? Name { get; init; }

    public ModuleArtifacts? Artifacts { get; init; }
}

[Validatable]
public sealed record ModuleArtifacts
(
    string? ExitCodeName,
    ModuleEnvironmentReference? Environment
);