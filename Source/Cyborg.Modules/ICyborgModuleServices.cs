using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
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
using Jab;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Modules;

[ServiceProviderModule]
[Singleton<ModuleJsonSerializerContext>(Factory = nameof(GetModuleJsonSerializerContext))]
[Singleton<JsonSerializerContext>(Factory = nameof(GetModuleJsonSerializerContext))]
[Singleton<JsonNamingPolicy>(Factory = nameof(GetModuleJsonNamingPolicy))]
[Singleton<IModuleLoader, SequenceModuleLoader>]
[Singleton<IModuleLoader, SubprocessModuleLoader>]
[Singleton<IModuleLoader, SwitchModuleLoader>]
[Singleton<IModuleLoader, ConfigMapModuleLoader>]
[Singleton<IModuleLoader, ConfigCollectionModuleLoader>]
[Singleton<IModuleLoader, ExternalConfigModuleLoader>]
[Singleton<IModuleLoader, NamedModuleReferenceModuleLoader>]
[Singleton<IModuleLoader, ForeachModuleLoader>]
[Singleton<IModuleLoader, WakeOnLanModuleLoader>]
[Singleton<IModuleLoader, IfModuleLoader>]
[Singleton<IModuleLoader, IsTrueModuleLoader>]
[Singleton<IModuleLoader, IsSetModuleLoader>]
[Singleton<IModuleLoader, GuardModuleLoader>]
[Singleton<IModuleLoader, GlobModuleLoader>]
[Singleton<IModuleLoader, ExternalModuleLoader>]
[Singleton<IModuleLoader, EnvironmentDefinitionsModuleLoader>]
[Singleton<IModuleLoader, SshShutdownModuleLoader>]
[Singleton<IModuleLoader, DynamicModuleLoader>]
[Singleton<IModuleLoader, AssertModuleLoader>]
[Singleton<IModuleLoader, TemplateModuleLoader>]
[Singleton<IModuleLoader, EmptyModuleLoader>]
[Singleton<JsonConverter>(Factory = nameof(CreateEnvironmentScopeReferenceConverter))]
public interface ICyborgModuleServices
{
    static ModuleJsonSerializerContext GetModuleJsonSerializerContext() => ModuleJsonSerializerContext.Default;

    static JsonNamingPolicy GetModuleJsonNamingPolicy() => JsonNamingPolicy.SnakeCaseLower;

    static JsonConverter CreateEnvironmentScopeReferenceConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<EnvironmentScopeReference>(namingPolicy);
}
