using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Core.Modules.Configuration.Model;

public sealed record ModuleEnvironment
(
    EnvironmentScope Scope = EnvironmentScope.Global,
    string? Name = null
);