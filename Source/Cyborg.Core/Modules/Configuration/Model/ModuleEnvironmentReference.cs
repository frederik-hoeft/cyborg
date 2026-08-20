using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules.Configuration.Model;

[Validatable]
public sealed record ModuleEnvironmentReference
(
    [property: DefinedEnumValue]
    [property: DefaultValue<EnvironmentScopeReference>(EnvironmentScopeReference.Current)]
    EnvironmentScopeReference Scope,
    [property: Untagged] string? Name
) : IDefaultInstance<ModuleEnvironmentReference>
{
    public static ModuleEnvironmentReference Default => new(EnvironmentScopeReference.Current, Name: null);
}
