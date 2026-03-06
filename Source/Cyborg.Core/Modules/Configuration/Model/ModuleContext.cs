namespace Cyborg.Core.Modules.Configuration.Model;

public record ModuleContext
(
    ModuleReference Module,
    ModuleEnvironment? Environment,
    ModuleReference? Configuration
);