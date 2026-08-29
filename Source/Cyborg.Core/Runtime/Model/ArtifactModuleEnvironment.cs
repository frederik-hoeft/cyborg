using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Validation;

namespace Cyborg.Core.Runtime.Model;

[Validatable]
public sealed record ArtifactModuleEnvironment : ModuleEnvironment, IDefaultInstance<ArtifactModuleEnvironment>
{
    [DefinedEnumValue]
    [DefaultValue<EnvironmentScope>(EnvironmentScope.InheritParent)]
    public override EnvironmentScope Scope { get; init; }

    public static new ArtifactModuleEnvironment Default => new() { Scope = EnvironmentScope.Parent };
}
