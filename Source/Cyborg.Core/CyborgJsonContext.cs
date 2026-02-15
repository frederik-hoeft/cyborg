using System.Text.Json.Serialization;
using Cyborg.Core.Configuration;
using Cyborg.Core.Logging;

namespace Cyborg.Core;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BackupJobConfiguration))]
[JsonSerializable(typeof(BackupHostConfiguration))]
[JsonSerializable(typeof(WakeOnLanConfiguration))]
[JsonSerializable(typeof(DockerConfiguration))]
[JsonSerializable(typeof(BorgConfiguration))]
[JsonSerializable(typeof(JsonLogEntry))]
public partial class CyborgJsonContext : JsonSerializerContext
{
}
