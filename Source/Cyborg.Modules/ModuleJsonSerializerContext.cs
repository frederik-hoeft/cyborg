using Cyborg.Core.Aot.Json.Configuration;
using Cyborg.Modules.Borg;
using Cyborg.Modules.Configuration.ConfigCollection;
using Cyborg.Modules.Configuration.ConfigMap;
using Cyborg.Modules.Foreach;
using Cyborg.Modules.Named;
using Cyborg.Modules.Network.WakeOnLan;
using Cyborg.Modules.Sequence;
using Cyborg.Modules.Subprocess;
using Cyborg.Modules.Template;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Modules;

[JsonTypeInfoBindingsGenerator(GenerationMode = BindingsGenerationMode.Optimized)]
[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, IncludeFields = true)]
[JsonSerializable(typeof(SequenceModule))]
[JsonSerializable(typeof(SubprocessModule))]
[JsonSerializable(typeof(TemplateModule))]
[JsonSerializable(typeof(ConfigMapModule))]
[JsonSerializable(typeof(ConfigCollectionModule))]
[JsonSerializable(typeof(NamedModuleDefinitionModule))]
[JsonSerializable(typeof(NamedModuleReferenceModule))]
[JsonSerializable(typeof(ForeachModule))]
[JsonSerializable(typeof(WakeOnLanModule))]
// TODO: temp
[JsonSerializable(typeof(BorgRemote))]
public sealed partial class ModuleJsonSerializerContext : AotJsonSerializerContext;
