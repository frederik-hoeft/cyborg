using Cyborg.Core.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Debugging.Configuration;
using Cyborg.Core.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Core;

[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, IncludeFields = true)]
[JsonSerializable(typeof(ModuleContextDeserializationDummy))]
[JsonSerializable(typeof(ConfigurationSource))]
[JsonSerializable(typeof(DebugOptions))]
[JsonSerializable(typeof(TaggedString))]
public sealed partial class CoreJsonSerializerContext : JsonSerializerContext;
