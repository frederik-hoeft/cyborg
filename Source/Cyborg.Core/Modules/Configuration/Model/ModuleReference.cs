using System.Text.Json.Serialization;

namespace Cyborg.Core.Modules.Configuration.Model;

public sealed record ModuleReference
(
    [property: JsonIgnore] IModule Definition,
    [property: JsonIgnore] string ModuleId
);
