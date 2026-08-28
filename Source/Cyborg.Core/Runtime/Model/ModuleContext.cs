using Cyborg.Core.Aot.Modules.Validation.Attributes;
using System.Text.Json.Serialization;

namespace Cyborg.Core.Runtime.Model;

[Validatable]
public partial record ModuleContext
(
    [property: Required] ModuleReference Module,
    [property: Required][property: DefaultInstance] ModuleEnvironment Environment,
    ModuleReference? Configuration,
    [property: Required][property: DefaultInstance] ModuleRequirements Requires
);

public partial record ModuleContext
{
    [JsonIgnore]
    internal ModuleRegistrySeed NamedModules { get; init; } = ModuleRegistrySeed.Empty;
}
