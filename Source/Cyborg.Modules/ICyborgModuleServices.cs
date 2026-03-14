using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Configuration.Serialization;
using Cyborg.Modules.If;
using Cyborg.Modules.Configuration.ConfigCollection;
using Cyborg.Modules.Configuration.ConfigMap;
using Cyborg.Modules.Named;
using Cyborg.Modules.Network.WakeOnLan;
using Cyborg.Modules.Sequence;
using Cyborg.Modules.Subprocess;
using Cyborg.Modules.Template;
using Jab;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cyborg.Modules.If.Conditions.IsTrue;
using Cyborg.Modules.Foreach;
using Cyborg.Modules.Glob;
using Cyborg.Modules.External;
using Cyborg.Modules.EnvironmentDefinitions;
using Cyborg.Modules.Network.SshShutdown;
using Cyborg.Modules.Dynamic;

namespace Cyborg.Modules;

[ServiceProviderModule]
[Singleton<ModuleJsonSerializerContext>(Factory = nameof(GetModuleJsonSerializerContext))]
[Singleton<JsonSerializerContext>(Factory = nameof(GetModuleJsonSerializerContext))]
[Singleton<JsonNamingPolicy>(Factory = nameof(GetModuleJsonNamingPolicy))]
[Singleton<IModuleLoader, SequenceModuleLoader>]
[Singleton<IModuleLoader, SubprocessModuleLoader>]
[Singleton<IModuleLoader, TemplateModuleLoader>]
[Singleton<IModuleLoader, ConfigMapModuleLoader>]
[Singleton<IModuleLoader, ConfigCollectionModuleLoader>]
[Singleton<IModuleLoader, NamedModuleReferenceModuleLoader>]
[Singleton<IModuleLoader, ForeachModuleLoader>]
[Singleton<IModuleLoader, WakeOnLanModuleLoader>]
[Singleton<IModuleLoader, IfModuleLoader>]
[Singleton<IModuleLoader, IsTrueModuleLoader>]
[Singleton<IModuleLoader, GlobModuleLoader>]
[Singleton<IModuleLoader, ExternalModuleLoader>]
[Singleton<IModuleLoader, EnvironmentDefinitionsModuleLoader>]
[Singleton<IModuleLoader, SshShutdownModuleLoader>]
[Singleton<IModuleLoader, DynamicModuleLoader>]
[Singleton<JsonConverter>(Factory = nameof(CreateEnvironmentScopeReferenceConverter))]
public interface ICyborgModuleServices
{
    static ModuleJsonSerializerContext GetModuleJsonSerializerContext() => ModuleJsonSerializerContext.Default;

    static JsonNamingPolicy GetModuleJsonNamingPolicy() => JsonNamingPolicy.SnakeCaseLower;

    static JsonConverter CreateEnvironmentScopeReferenceConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<EnvironmentScopeReference>(namingPolicy);
}
