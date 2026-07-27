using Cyborg.Cli.Debugging;
using Cyborg.Cli.Debugging.Commands;
using Cyborg.Cli.Logging;
using Cyborg.Cli.Logging.Options;
using Cyborg.Cli.Metrics;
using Cyborg.Core.Configuration;
using Cyborg.Core.Configuration.Serialization.Dynamics;
using Cyborg.Core.Logging;
using Cyborg.Core.Modules.Debugging;
using Jab;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZLogger.Providers;

namespace Cyborg.Cli;

[ServiceProviderModule]
[Singleton<LoggingOptions>]
[Singleton<ILoggingConfigurator, ConsoleLoggingConfigurator>]
[Singleton<ILoggingConfigurator, RollingFileLoggingConfigurator>]
[Singleton<ILoggingConfigurator, FileLoggingConfigurator>]
[Singleton<IDynamicValueProvider, DynamicGlobalLoggingOptionsProvider>]
[Singleton<IDynamicValueProvider, DynamicRollingFileLoggingConfiguratorOptionsProvider>]
[Singleton<IDynamicValueProvider, DynamicFileLoggingConfiguratorOptionsProvider>]
[Singleton<IDynamicValueProvider, DynamicConsoleLoggingConfiguratorOptionsProvider>]
[Singleton<IDynamicValueProvider, DynamicMetricsOptionsProvider>]
[Singleton<ILoggerFactory>(Factory = nameof(CreateLoggerFactory))]
[Singleton<JsonConverter>(Factory = nameof(CreateRollingIntervalConverter))]
[Singleton<JsonConverter>(Factory = nameof(CreateLogFormatConverter))]
[Singleton<JsonConverter>(Factory = nameof(CreateLogLevelConverter))]
[Singleton<IDebugReplIo, ConsoleDebugReplIo>]
[Singleton<IDebugReplCommand, ContinueCommand>]
[Singleton<IDebugReplCommand, DetachCommand>]
[Singleton<IDebugReplCommand, StepCommand>]
[Singleton<IDebugReplCommand, InspectCommand>]
[Singleton<IDebugReplCommand, CancelCommand>]
[Singleton<IDebugReplCommand, BreakCommand>]
[Singleton<IDebugFrontend, ConsoleDebugFrontend>]
internal interface ICyborgCliServiceOptions
{
    static ILoggerFactory CreateLoggerFactory(IConfiguration configuration, IEnumerable<ILoggingConfigurator> configurators) =>
        LoggerFactory.Create(builder =>
        {
            GlobalLoggingOptions globalOptions = configuration.Get("cyborg.services.logging", () => new GlobalLoggingOptions(LogLevel.Trace));
            builder.SetMinimumLevel(globalOptions.MinimumLevel);
            foreach (ILoggingConfigurator configurator in configurators)
            {
                configurator.Configure(builder);
            }
        });

    static JsonConverter CreateRollingIntervalConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<RollingInterval>(namingPolicy);

    static JsonConverter CreateLogFormatConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<LogFormat>(namingPolicy);

    static JsonConverter CreateLogLevelConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<LogLevel>(namingPolicy);
}
