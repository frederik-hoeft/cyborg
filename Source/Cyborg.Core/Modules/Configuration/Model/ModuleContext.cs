using Cyborg.Core.Aot.Modules.Validation.Attributes;

namespace Cyborg.Core.Modules.Configuration.Model;

[Validatable]
public record ModuleContext
(
    [property: Required] ModuleReference Module,
    [property: Required][property: DefaultInstance] ModuleEnvironment Environment,
    ModuleReference? Configuration,
    [property: Required][property: DefaultInstance] ModuleTemplateDefinition Template
);