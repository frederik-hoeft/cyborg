using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules.Configuration.Model;

[Validatable]
public record ModuleEnvironment
(
    [property: DefaultValue<EnvironmentScope>(EnvironmentScope.InheritParent)][property: DefinedEnumValue] EnvironmentScope Scope,
    string? Name
) : IDefaultInstance<ModuleEnvironment>
{
    public static ModuleEnvironment Default => new(EnvironmentScope.InheritParent, Name: null);
}