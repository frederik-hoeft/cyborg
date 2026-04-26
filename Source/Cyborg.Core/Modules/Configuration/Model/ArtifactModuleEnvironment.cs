using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules.Configuration.Model;

[Validatable]
public sealed record ArtifactModuleEnvironment : ModuleEnvironment, IDefaultInstance<ArtifactModuleEnvironment>
{
    [DefinedEnumValue]
    [DefaultValue<EnvironmentScope>(EnvironmentScope.InheritParent)]
    public override EnvironmentScope Scope { get; init; }

    public static new ArtifactModuleEnvironment Default => new() { Scope = EnvironmentScope.Parent };
}
