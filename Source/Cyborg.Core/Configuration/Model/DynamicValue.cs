using System.Text.Json.Serialization;

namespace Cyborg.Core.Configuration.Model;

public sealed record DynamicValue([property: JsonIgnore] object Value);