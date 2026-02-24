using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Modules.Shared.Model;

public sealed record ModuleEnvironment
(
    EnvironmentScope Scope = EnvironmentScope.Global,
    string? Name = null
);