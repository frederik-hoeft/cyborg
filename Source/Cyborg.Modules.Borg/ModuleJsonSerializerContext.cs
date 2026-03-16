using Cyborg.Modules.Borg.Create;
using Cyborg.Modules.Borg.Prune;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Modules.Borg;

[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, IncludeFields = true)]
[JsonSerializable(typeof(BorgRemote))]
[JsonSerializable(typeof(BorgCreateModule))]
[JsonSerializable(typeof(BorgPruneModule))]
public sealed partial class BorgJsonSerializerContext : JsonSerializerContext;