using Cyborg.Core.Text;
using Cyborg.Modules.Borg.Compact;
using Cyborg.Modules.Borg.Create;
using Cyborg.Modules.Borg.Create.Model;
using Cyborg.Modules.Borg.Prune;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Modules.Borg;

[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, IncludeFields = true)]
[JsonSerializable(typeof(BorgRemote))]
[JsonSerializable(typeof(BorgRemoteRepository))]
[JsonSerializable(typeof(BorgExcludeOptions))]
[JsonSerializable(typeof(BorgFilesCacheSentinelOptions))]
[JsonSerializable(typeof(BorgCreateModule))]
[JsonSerializable(typeof(BorgPruneModule))]
[JsonSerializable(typeof(BorgCompactModule))]
[JsonSerializable(typeof(TaggedString))]
public sealed partial class BorgJsonSerializerContext : JsonSerializerContext;
