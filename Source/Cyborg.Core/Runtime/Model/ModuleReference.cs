using System.Text.Json.Serialization;

namespace Cyborg.Core.Runtime.Model;

public sealed record ModuleReference
(
    [property: JsonIgnore] IModule Definition,
    [property: JsonIgnore] string ModuleId
);
