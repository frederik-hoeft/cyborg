using Cyborg.Core.Aot.Modules.Validation.Model;

namespace Cyborg.Core.Modules.Configuration.Model;

[Validatable]
public record ModuleContext
(
    [property: Required] ModuleReference Module,
    [property: DefaultInstance] ModuleEnvironment? Environment,
    ModuleReference? Configuration
);