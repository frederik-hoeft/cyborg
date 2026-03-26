using Cyborg.Modules.Assert;
using Cyborg.Modules.Configuration.ConfigCollection;
using Cyborg.Modules.Configuration.ConfigMap;
using Cyborg.Modules.Configuration.ExternalConfig;
using Cyborg.Modules.Dynamic;
using Cyborg.Modules.Empty;
using Cyborg.Modules.EnvironmentDefinitions;
using Cyborg.Modules.External;
using Cyborg.Modules.Foreach;
using Cyborg.Modules.Glob;
using Cyborg.Modules.Guard;
using Cyborg.Modules.If;
using Cyborg.Modules.If.Conditions.IsSet;
using Cyborg.Modules.If.Conditions.IsTrue;
using Cyborg.Modules.Named;
using Cyborg.Modules.Network.SshShutdown;
using Cyborg.Modules.Network.WakeOnLan;
using Cyborg.Modules.Sequence;
using Cyborg.Modules.Subprocess;
using Cyborg.Modules.Switch;
using Cyborg.Modules.Template;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Modules;

[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, IncludeFields = true)]
[JsonSerializable(typeof(SequenceModule))]
[JsonSerializable(typeof(SubprocessModule))]
[JsonSerializable(typeof(SwitchModule))]
[JsonSerializable(typeof(ConfigMapModule))]
[JsonSerializable(typeof(ConfigCollectionModule))]
[JsonSerializable(typeof(ExternalConfigModule))]
[JsonSerializable(typeof(NamedModuleReferenceModule))]
[JsonSerializable(typeof(ForeachModule))]
[JsonSerializable(typeof(WakeOnLanModule))]
[JsonSerializable(typeof(IfModule))]
[JsonSerializable(typeof(IsTrueModule))]
[JsonSerializable(typeof(IsSetModule))]
[JsonSerializable(typeof(GlobModule))]
[JsonSerializable(typeof(ExternalModule))]
[JsonSerializable(typeof(EnvironmentDefinitionsModule))]
[JsonSerializable(typeof(SshShutdownModule))]
[JsonSerializable(typeof(DynamicModule))]
[JsonSerializable(typeof(AssertModule))]
[JsonSerializable(typeof(TemplateModule))]
[JsonSerializable(typeof(GuardModule))]
[JsonSerializable(typeof(EmptyModule))]
public sealed partial class ModuleJsonSerializerContext : JsonSerializerContext;
