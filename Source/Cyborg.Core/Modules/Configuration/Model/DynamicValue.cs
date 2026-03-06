using System.Text.Json.Serialization;

namespace Cyborg.Core.Modules.Configuration.Model;

public sealed record DynamicValue([property: JsonIgnore] object Value);