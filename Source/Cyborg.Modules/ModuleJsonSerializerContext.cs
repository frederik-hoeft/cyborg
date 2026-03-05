using Cyborg.Core.Aot.Json.Configuration;
using Cyborg.Modules.Configuration.ConfigCollection;
using Cyborg.Modules.Configuration.ConfigMap;
using Cyborg.Modules.Sequence;
using Cyborg.Modules.Subprocess;
using Cyborg.Modules.Template;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Modules;

[JsonTypeInfoBindingsGenerator(GenerationMode = BindingsGenerationMode.Optimized)]
[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(SequenceModule))]
[JsonSerializable(typeof(SubprocessModule))]
[JsonSerializable(typeof(TemplateModule))]
[JsonSerializable(typeof(ConfigMapModule))]
[JsonSerializable(typeof(ConfigCollectionModule))]
public sealed partial class ModuleJsonSerializerContext : AotJsonSerializerContext;
