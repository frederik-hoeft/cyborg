using Cyborg.Modules.Borg;
using Cyborg.Modules.Configuration.ConfigCollection;
using Cyborg.Modules.Configuration.ConfigMap;
using Cyborg.Modules.EnvironmentDefinitions;
using Cyborg.Modules.External;
using Cyborg.Modules.Foreach;
using Cyborg.Modules.Glob;
using Cyborg.Modules.If;
using Cyborg.Modules.If.Conditions.IsTrue;
using Cyborg.Modules.Named;
using Cyborg.Modules.Network.SshShutdown;
using Cyborg.Modules.Network.WakeOnLan;
using Cyborg.Modules.Sequence;
using Cyborg.Modules.Subprocess;
using Cyborg.Modules.Template;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Modules;

[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, IncludeFields = true)]
[JsonSerializable(typeof(SequenceModule))]
[JsonSerializable(typeof(SubprocessModule))]
[JsonSerializable(typeof(TemplateModule))]
[JsonSerializable(typeof(ConfigMapModule))]
[JsonSerializable(typeof(ConfigCollectionModule))]
[JsonSerializable(typeof(NamedModuleReferenceModule))]
[JsonSerializable(typeof(ForeachModule))]
[JsonSerializable(typeof(WakeOnLanModule))]
[JsonSerializable(typeof(IfModule))]
[JsonSerializable(typeof(IsTrueModule))]
[JsonSerializable(typeof(GlobModule))]
[JsonSerializable(typeof(ExternalModule))]
[JsonSerializable(typeof(EnvironmentDefinitionsModule))]
[JsonSerializable(typeof(SshShutdownModule))]
// TODO: temp
[JsonSerializable(typeof(BorgRemote))]
public sealed partial class ModuleJsonSerializerContext : JsonSerializerContext;
