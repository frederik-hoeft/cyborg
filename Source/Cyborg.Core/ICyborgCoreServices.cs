using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Services;
using Jab;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Core;

[ServiceProviderModule]
[Singleton<INamedServiceProvider, NamedServiceProvider>]
[Singleton<IModuleLoaderContext, DefaultModuleLoaderContext>]
[Singleton<JsonConverter, ModuleReferenceJsonConverter>]
[Singleton<JsonConverter>(Factory = nameof(CreateEnvironmentScopeConverter))]
[Singleton<IModuleLoaderRegistry, DefaultModuleLoaderRegistry>]
[Singleton<IModuleWorkerFactory, DefaultModuleWorkerFactory>]
[Singleton<IModuleConfigurationLoader, DefaultModuleConfigurationLoader>]
[Singleton<IModuleRuntime, ModuleRuntime>]
[Singleton<GlobalRuntimeEnvironment>]
public interface ICyborgCoreServices
{
    static JsonConverter CreateEnvironmentScopeConverter() => new JsonStringEnumConverter<EnvironmentScope>(JsonNamingPolicy.SnakeCaseLower);
}