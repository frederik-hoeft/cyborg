using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Configuration.Serialization;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Services;
using Cyborg.Core.Services.Network.Probe;
using Cyborg.Core.Services.Subprocesses;
using Jab;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Core;

[ServiceProviderModule]
[Import<IDynamicValueProviderServices>]
[Singleton<INamedServiceProvider, NamedServiceProvider>]
[Singleton<IModuleLoaderContext, DefaultModuleLoaderContext>]
[Singleton<JsonConverter, ModuleReferenceJsonConverter>]
[Singleton<JsonConverter, DynamicValueJsonConverter>]
[Singleton<JsonConverter, DynamicKeyValuePairJsonConverter>]
[Singleton<JsonConverter>(Factory = nameof(CreateEnvironmentScopeConverter))]
[Singleton<IModuleLoaderRegistry, DefaultModuleLoaderRegistry>]
[Singleton<IModuleWorkerFactory, DefaultModuleWorkerFactory>]
[Singleton<IModuleConfigurationLoader, DefaultModuleConfigurationLoader>]
[Singleton<IModuleRuntime, ModuleRuntime>]
[Singleton<IModuleRegistry, DefaultModuleRegistry>]
[Singleton<ISubprocessDispatcher, DefaultSubprocessDispatcher>]
[Singleton<IPingService, DefaultPingService>]
[Singleton<GlobalRuntimeEnvironment>]
public interface ICyborgCoreServices
{
    static JsonConverter CreateEnvironmentScopeConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<EnvironmentScope>(namingPolicy);
}