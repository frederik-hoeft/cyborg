using System.Text.Json.Serialization;

namespace Cyborg.Core.Modules.Configuration.Model;

public sealed record DynamicKeyValuePair(string Key, [property: JsonIgnore] object Value);