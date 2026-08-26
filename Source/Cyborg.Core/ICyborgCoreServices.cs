using Cyborg.Core.Configuration;
using Cyborg.Core.Configuration.Builders;
using Cyborg.Core.Configuration.Loaders;
using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Configuration.Serialization;
using Cyborg.Core.Configuration.Serialization.Dynamics;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Debugging;
using Cyborg.Core.Modules.Descriptors;
using Cyborg.Core.Modules.Hooks;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Artifacts;
using Cyborg.Core.Services;
using Cyborg.Core.Services.Dispatch;
using Cyborg.Core.Services.Metrics;
using Cyborg.Core.Services.Network.Probe;
using Cyborg.Core.Services.Pipelines;
using Cyborg.Core.Services.Security.Trust;
using Cyborg.Core.Text;
using Jab;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Core;

[ServiceProviderModule]
[Import<IDynamicValueProviderServices>]
[Import<IConfigurationTrustServices>]
[Import<IModuleDescriptionServices>]
[Import<IDebugServices>]
[Import<ITaggedStringServices>]
[Singleton<IConfiguration, DefaultConfiguration>]
[Transient<IConfigurationBuilder, DefaultConfigurationBuilder>]
[Transient<IConfigurationFileLoader, ConfigurationFileLoader>]
[Transient<IConfigurationDictionaryLoader, ConfigurationDictionaryLoader>]
[Singleton<INamedServiceProvider, NamedServiceProvider>]
[Singleton<IJsonLoaderContext, DefaultJsonLoaderContext>]
[Singleton<IJsonLoaderContextProvider, DefaultJsonLoaderContextProvider>]
[Singleton<IDynamicValueProvider, ModuleContextDynamicProvider>]
[Singleton<IDynamicValueProvider, ModuleEnvironmentDynamicProvider>]
[Singleton<IDynamicValueProvider, ModuleReferenceDynamicProvider>]
[Singleton<JsonConverter, ModuleReferenceJsonConverter>]
[Singleton<JsonConverter, DynamicValueJsonConverter>]
[Singleton<JsonConverter, DynamicKeyValuePairJsonConverter>]
[Singleton<JsonConverter, ModuleContextJsonConverter>]
[Singleton<JsonConverter, TaggedStringJsonConverter>]
[Singleton<JsonConverter>(Factory = nameof(CreateEnvironmentScopeConverter))]
[Singleton<JsonConverter>(Factory = nameof(CreateDecompositionStrategyConverter))]
[Singleton<IModuleLoaderRegistry, DefaultModuleLoaderRegistry>]
[Singleton<IModuleWorkerFactory, DefaultModuleWorkerFactory>]
[Singleton<IModuleConfigurationLoader, DefaultModuleConfigurationLoader>]
[Transient<IModuleRuntime>(Factory = nameof(CreateRootModuleRuntime))]
[Singleton<IModuleRegistry, DefaultModuleRegistry>]
[Singleton<IModuleArtifactsFactory, DefaultModuleArtifactsFactory>]
[Transient<IServicePipeline<IModuleValidationHook>, ServicePipeline<IModuleValidationHook>>]
[Transient<IServicePipeline<IModulePreExecutionHook>, ServicePipeline<IModulePreExecutionHook>>]
[Transient<IServicePipeline<IModulePostExecutionHook>, ServicePipeline<IModulePostExecutionHook>>]
[Singleton<IChildProcessDispatcher, DefaultChildProcessDispatcher>]
[Singleton<IPingService, DefaultPingService>]
[Singleton<IPortProbeService, TcpPortProbeService>]
[Singleton<IPosixShellCommandBuilder, PosixShellCommandBuilder>]
[Singleton<IModuleResultBuilderFactory, ModuleResultBuilderFactory>]
[Singleton<MetricsCollectorOptions>]
[Singleton<IMetricsCollector, MetricsCollector>]
[Singleton<JsonSerializerContext>(Factory = nameof(GetCoreJsonSerializerContext))]
public interface ICyborgCoreServices
{
    static CoreJsonSerializerContext GetCoreJsonSerializerContext() => CoreJsonSerializerContext.Default;

    static JsonConverter CreateEnvironmentScopeConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<EnvironmentScope>(namingPolicy);

    static JsonConverter CreateDecompositionStrategyConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<DecompositionStrategy>(namingPolicy);

    static IModuleRuntime CreateRootModuleRuntime(
        JsonNamingPolicy namingPolicy,
        ITaggedStringConversionObserver taggedStringConversionObserver,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider)
    {
        GlobalRuntimeEnvironment globalEnvironment = new(namingPolicy);
        return new RootModuleRuntime(globalEnvironment, taggedStringConversionObserver, loggerFactory, serviceProvider);
    }
}
