using Cyborg.Modules.Borg.Shared.Json.Create;
using Cyborg.Modules.Borg.Shared.Json.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Modules.Borg.Shared.Json;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(BorgCreateJsonResult))]
[JsonSerializable(typeof(BorgJsonLineHeader))]
[JsonSerializable(typeof(BorgLogMessageJsonLine))]
[JsonSerializable(typeof(BorgCreateJsonResult))]
public sealed partial class BorgOutputJsonSerializerContext : JsonSerializerContext;