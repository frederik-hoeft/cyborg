using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Modules.Borg;

[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, IncludeFields = true)]
[JsonSerializable(typeof(BorgRemote))]
public sealed partial class BorgJsonSerializerContext : JsonSerializerContext;