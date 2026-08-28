using Cyborg.Core.Configuration;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Runtime.Services.Debugging.Configuration;
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
