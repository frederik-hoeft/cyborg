using System.Text.Json.Serialization;

namespace Cyborg.Core.Runtime.Model;

/// <summary>
/// A module definition together with the id used to activate it.
/// <see cref="Origin"/> is definition-site provenance. It is null when the reference was not loaded from a file and the caller did not supply one.
/// </summary>
public sealed record ModuleReference
(
    [property: JsonIgnore] IModule Definition,
    [property: JsonIgnore] string ModuleId,
    [property: JsonIgnore] ModuleOrigin? Origin = null
);
