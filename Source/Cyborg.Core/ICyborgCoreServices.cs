using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Configuration.Serialization;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Artifacts;
using Cyborg.Core.Services;
using Cyborg.Core.Services.Dispatch;
using Cyborg.Core.Services.Metrics;
using Cyborg.Core.Services.Network.Probe;
using Jab;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Core;

[ServiceProviderModule]
[Import<IDynamicValueProviderServices>]
[Singleton<INamedServiceProvider, NamedServiceProvider>]
[Singleton<IModuleLoaderContext, DefaultModuleLoaderContext>]
[Singleton<IModuleLoaderContextProvider, DefaultModuleLoaderContextProvider>]
[Singleton<JsonConverter, ModuleReferenceJsonConverter>]
[Singleton<JsonConverter, DynamicValueJsonConverter>]
[Singleton<JsonConverter, DynamicKeyValuePairJsonConverter>]
[Singleton<JsonConverter, ModuleContextJsonConverter>]
[Singleton<JsonConverter>(Factory = nameof(CreateEnvironmentScopeConverter))]
[Singleton<JsonConverter>(Factory = nameof(CreateDecompositionStrategyConverter))]
[Singleton<IModuleLoaderRegistry, DefaultModuleLoaderRegistry>]
[Singleton<IModuleWorkerFactory, DefaultModuleWorkerFactory>]
[Singleton<IModuleConfigurationLoader, DefaultModuleConfigurationLoader>]
[Singleton<IModuleRuntime, RootModuleRuntime>]
[Singleton<IModuleRegistry, DefaultModuleRegistry>]
[Singleton<IModuleArtifactsFactory, DefaultModuleArtifactsFactory>]
[Singleton<IChildProcessDispatcher, DefaultChildProcessDispatcher>]
[Singleton<IPingService, DefaultPingService>]
[Singleton<IPortProbeService, TcpPortProbeService>]
[Singleton<IPosixShellCommandBuilder, PosixShellCommandBuilder>]
[Singleton<MetricsCollectorOptions>]
[Singleton<IMetricsCollector, MetricsCollector>]
[Singleton<JsonSerializerContext>(Factory = nameof(GetCoreJsonSerializerContext))]
[Singleton<GlobalRuntimeEnvironment>]
public interface ICyborgCoreServices
{
    static CoreJsonSerializerContext GetCoreJsonSerializerContext() => CoreJsonSerializerContext.Default;

    static JsonConverter CreateEnvironmentScopeConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<EnvironmentScope>(namingPolicy);

    static JsonConverter CreateDecompositionStrategyConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<DecompositionStrategy>(namingPolicy);
}