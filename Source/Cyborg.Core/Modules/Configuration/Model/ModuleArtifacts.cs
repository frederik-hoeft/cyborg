using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules.Runtime.Artifacts;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules.Configuration.Model;

[Validatable]
public sealed record ModuleArtifacts
(
    string? CustomNamespace,
    [property: Required][property: DefaultValue<string>(Constants.DEFAULT_STATUS_CODE_NAME)] string ExitStatusName,
    [property: DefaultInstance] ModuleEnvironmentReference Environment,
    [property: DefaultValue<DecompositionStrategy>(DecompositionStrategy.LeavesOnly)] DecompositionStrategy DecompositionStrategy,
    bool PublishNullValues
) : IDefaultInstance<ModuleArtifacts>
{
    public static ModuleArtifacts Default => new(CustomNamespace: null, ExitStatusName: Constants.DEFAULT_STATUS_CODE_NAME, Environment: default!, DecompositionStrategy.LeavesOnly, PublishNullValues: false);
}

file static class Constants
{
    public const string DEFAULT_STATUS_CODE_NAME = "$?";
}