using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Configuration.Serialization;
using Cyborg.Modules.Borg;
using Cyborg.Modules.Conditional;
using Cyborg.Modules.Configuration.ConfigCollection;
using Cyborg.Modules.Configuration.ConfigMap;
using Cyborg.Modules.Foreach;
using Cyborg.Modules.Named;
using Cyborg.Modules.Network.WakeOnLan;
using Cyborg.Modules.Sequence;
using Cyborg.Modules.Subprocess;
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
[Singleton<IModuleLoader, TemplateModuleLoader>]
[Singleton<IModuleLoader, ConfigMapModuleLoader>]
[Singleton<IModuleLoader, ConfigCollectionModuleLoader>]
[Singleton<IModuleLoader, NamedModuleDefinitionModuleLoader>]
[Singleton<IModuleLoader, NamedModuleReferenceModuleLoader>]
[Singleton<IModuleLoader, ForeachModuleLoader>]
[Singleton<IModuleLoader, WakeOnLanModuleLoader>]
[Singleton<IModuleLoader, IfModuleLoader>]
[Singleton<IDynamicValueProvider, BorgRemoteValueProvider>]
[Singleton<JsonConverter>(Factory = nameof(CreateEnvironmentScopeReferenceConverter))]
public interface ICyborgModuleServices
{
    static ModuleJsonSerializerContext GetModuleJsonSerializerContext() => ModuleJsonSerializerContext.Default;

    static JsonNamingPolicy GetModuleJsonNamingPolicy() => JsonNamingPolicy.SnakeCaseLower;

    static JsonConverter CreateEnvironmentScopeReferenceConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<EnvironmentScopeReference>(namingPolicy);
}
