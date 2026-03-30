using Cyborg.Cli.Logging.Options;
using Cyborg.Core.Logging;
using Cyborg.Core.Modules.Configuration.Serialization;
using Jab;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZLogger.Providers;

namespace Cyborg.Cli.Logging;

[ServiceProviderModule]
[Singleton<ILoggingConfigurator, ConsoleLoggingConfigurator>]
[Singleton<ILoggingConfigurator, RollingFileLoggingConfigurator>]
[Singleton<ILoggingConfigurator, FileLoggingConfigurator>]
[Singleton<IDynamicValueProvider, DynamicRollingFileLoggingConfiguratorOptionsProvider>]
[Singleton<IDynamicValueProvider, DynamicFileLoggingConfiguratorOptionsProvider>]
[Singleton<IDynamicValueProvider, DynamicConsoleLoggingConfiguratorOptionsProvider>]
[Singleton<ILoggerFactory>(Factory = nameof(CreateLoggerFactory))]
[Singleton<JsonConverter>(Factory = nameof(CreateRollingIntervalConverter))]
[Singleton<JsonConverter>(Factory = nameof(CreateLogFormatConverter))]
internal interface ICyborgCliLoggingServices
{
    static ILoggerFactory CreateLoggerFactory(IEnumerable<ILoggingConfigurator> configurators) =>
        LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            foreach (ILoggingConfigurator configurator in configurators)
            {
                configurator.Configure(builder);
            }
        });

    static JsonConverter CreateRollingIntervalConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<RollingInterval>(namingPolicy);

    static JsonConverter CreateLogFormatConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<LogFormat>(namingPolicy);
}