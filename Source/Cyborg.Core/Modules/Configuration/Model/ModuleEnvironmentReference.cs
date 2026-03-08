namespace Cyborg.Core.Modules.Configuration.Model;

public sealed record ModuleEnvironmentReference
(
    EnvironmentScopeReference Scope = EnvironmentScopeReference.Current,
    string? Name = null
);