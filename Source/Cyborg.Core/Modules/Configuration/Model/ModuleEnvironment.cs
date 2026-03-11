using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules.Configuration.Model;

[Validatable]
public sealed record ModuleEnvironment
(
    [property: DefaultValue<EnvironmentScope>(EnvironmentScope.Global)] EnvironmentScope Scope,
    string? Name
) : IDefaultInstance<ModuleEnvironment>
{
    public static ModuleEnvironment Default => new(EnvironmentScope.Global, Name: null);
}