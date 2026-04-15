using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules.Configuration.Model;

[Validatable]
public record ModuleEnvironment : IDefaultInstance<ModuleEnvironment>
{
    [DefinedEnumValue]
    [DefaultValue<EnvironmentScope>(EnvironmentScope.InheritParent)]
    public virtual EnvironmentScope Scope { get; init; }

    public virtual string? Name { get; init; }

    public virtual bool Transient { get; init; }

    public static ModuleEnvironment Default => new() { Scope = EnvironmentScope.InheritParent, };
}