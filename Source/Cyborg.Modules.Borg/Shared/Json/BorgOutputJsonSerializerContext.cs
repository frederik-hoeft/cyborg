using Cyborg.Modules.Borg.Create.Metrics;
using Cyborg.Modules.Borg.Shared.Json.Logging;
using System.Text.Json.Serialization;

namespace Cyborg.Modules.Borg.Shared.Json;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(BorgCreateJsonResult))]
[JsonSerializable(typeof(BorgJsonLineHeader))]
[JsonSerializable(typeof(BorgLogMessageJsonLine))]
public sealed partial class BorgOutputJsonSerializerContext : JsonSerializerContext;