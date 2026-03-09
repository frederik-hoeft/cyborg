using Cyborg.Core.Aot.Modules.Validation.Attributes;

namespace Cyborg.Modules.Shared.Model;

public sealed record ModuleEnvironmentReference
(
    [property: DefaultValue<EnvironmentScopeReference>(EnvironmentScopeReference.Current)] EnvironmentScopeReference Scope,
    string? Name
);