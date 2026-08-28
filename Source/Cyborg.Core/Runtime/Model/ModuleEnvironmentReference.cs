using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Runtime.Services.Validation;

namespace Cyborg.Core.Runtime.Model;

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
