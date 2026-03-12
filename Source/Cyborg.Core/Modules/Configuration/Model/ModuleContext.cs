using Cyborg.Core.Aot.Modules.Validation.Attributes;

namespace Cyborg.Core.Modules.Configuration.Model;

// TODO: link this to the module to be able to resolve named modules to their contexts (e.g., for referenced execution)
[Validatable]
public record ModuleContext
(
    [property: Required] ModuleReference Module,
    [property: DefaultInstance] ModuleEnvironment? Environment,
    ModuleReference? Configuration
);