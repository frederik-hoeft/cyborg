using Cyborg.Core.Aot.Json.Configuration;
using System.Text.Json.Serialization;
using Jab;
using Cyborg.Modules.Subprocess;
using Cyborg.Modules.Sequence;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Modules.Template;

namespace Cyborg.Modules;

[ServiceProviderModule]
[Singleton<ModuleJsonSerializerContext>(Factory = nameof(GetModuleJsonSerializerContext))]
[Singleton<JsonSerializerContext>(Factory = nameof(GetModuleJsonSerializerContext))]
[Singleton<IJsonTypeInfoProvider>(Factory = nameof(GetModuleJsonSerializerContext))]
[Singleton<IModuleLoader, SequenceModuleLoader>]
[Singleton<IModuleLoader, SubprocessModuleLoader>]
[Singleton<IModuleLoader, TemplateModuleLoader>]
public interface ICyborgModuleServices
{
    static ModuleJsonSerializerContext GetModuleJsonSerializerContext() => ModuleJsonSerializerContext.Default;
}
