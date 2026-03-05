using Cyborg.Core.Aot.Json.Configuration;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Modules.Configuration.ConfigCollection;
using Cyborg.Modules.Configuration.ConfigMap;
using Cyborg.Modules.Sequence;
using Cyborg.Modules.Subprocess;
using Cyborg.Modules.Template;
using Jab;
using System.Text.Json.Serialization;

namespace Cyborg.Modules;

[ServiceProviderModule]
[Singleton<ModuleJsonSerializerContext>(Factory = nameof(GetModuleJsonSerializerContext))]
[Singleton<JsonSerializerContext>(Factory = nameof(GetModuleJsonSerializerContext))]
[Singleton<IJsonTypeInfoProvider>(Factory = nameof(GetModuleJsonSerializerContext))]
[Singleton<IModuleLoader, SequenceModuleLoader>]
[Singleton<IModuleLoader, SubprocessModuleLoader>]
[Singleton<IModuleLoader, TemplateModuleLoader>]
[Singleton<IModuleLoader, ConfigMapModuleLoader>]
[Singleton<IModuleLoader, ConfigCollectionModuleLoader>]
public interface ICyborgModuleServices
{
    static ModuleJsonSerializerContext GetModuleJsonSerializerContext() => ModuleJsonSerializerContext.Default;
}
