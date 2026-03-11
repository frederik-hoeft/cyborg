using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules.Configuration.Model;

[Validatable]
public sealed record ModuleArtifacts
(
    string? SuccessCodeName,
    [property: DefaultInstance] ModuleEnvironmentReference Environment
) : IDefaultInstance<ModuleArtifacts>
{
    public static ModuleArtifacts Default => new(SuccessCodeName: null, Environment: default!);
}