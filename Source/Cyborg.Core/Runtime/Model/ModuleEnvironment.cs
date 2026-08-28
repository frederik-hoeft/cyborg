using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Validation;

namespace Cyborg.Core.Runtime.Model;

[Validatable]
public record ModuleEnvironment : IDefaultInstance<ModuleEnvironment>
{
    [DefinedEnumValue]
    [DefaultValue<EnvironmentScope>(EnvironmentScope.InheritParent)]
    public virtual EnvironmentScope Scope { get; init; }

    [Untagged]
    public virtual string? Name { get; init; }

    public virtual bool Transient { get; init; }

    public static ModuleEnvironment Default => new() { Scope = EnvironmentScope.InheritParent, };
}
