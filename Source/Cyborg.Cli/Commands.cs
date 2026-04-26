using ConsoleAppFramework;
using Cyborg.Cli.Arguments;
using Cyborg.Cli.Logging;
using Cyborg.Cli.Metrics;
using Cyborg.Core.Configuration;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Extensions;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Services.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Cli;

[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Must be instance method for ConsoleAppFramework.")]
internal sealed class Commands
{
    private const string CYBORG_ROOT = "/etc/cyborg";

    private static string QuoteArg(string arg) => $"\"{arg.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

    /// <summary>
    /// Executes a backup run using the provided configuration and command-line options.
    /// </summary>
    /// <remarks>
    /// This method loads configuration, sets up the runtime environment, executes the configured main module, and writes metrics output. Logging and metrics behavior can be customized via parameters or configuration files. If the run fails and file logging is enabled, the log file is written to standard output.
    /// </remarks>
    /// <param name="main">The file path to the main module configuration. Defaults to the primary configuration file if not specified.</param>
    /// <param name="options">The file path to the options configuration. Defaults to the standard options file if not specified.</param>
    /// <param name="environmentVariables">-e, An optional array of environment variable assignments to inject into the global environment, where each element must be in the format "key[:type]=value". The type is optional and must be an identifier of a supported dynamic value provider. If no type is specified, the value is treated as a literal string. When a type is specified, the value must be a valid JSON literal for the selected provider.</param>
    /// <param name="metrics">The file path where metrics output will be written. If null, the default metrics file path from configuration is used.</param>
    /// <param name="logLevel">The minimum log level to use for console output. If null, the default log level from configuration is used.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation and yields the process exit code.</returns>
    [Command("run")]
    public async Task<int> RunAsync(
        string main = $"{CYBORG_ROOT}/cyborg.jconf",
        string options = $"{CYBORG_ROOT}/cyborg.options.jconf",
        string[]? environmentVariables = null,
        string? metrics = null,
        LogLevel? logLevel = null,
        CancellationToken cancellationToken = default)
    {
        using DefaultServiceProvider services = new();
        IConfiguration configuration = services.GetRequiredService<IConfiguration>();
        IConfigurationLoader configurationLoader = services.GetRequiredService<IConfigurationLoader>();
        await configurationLoader.AddSourceAsync(configuration, options, cancellationToken);

        // CLI --log-level overrides only the console sink minimum level.
        if (logLevel.HasValue)
        {
            services.GetRequiredService<LoggingOptions>().MinimumLevel = logLevel.Value;
        }

        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("cyborg.cli.main");
        logger.LogStartup(string.Join(' ', Array.ConvertAll(Environment.GetCommandLineArgs()[1..], QuoteArg)));
        GlobalRuntimeEnvironment globalEnvironment = services.GetRequiredService<GlobalRuntimeEnvironment>();

        IEnvironmentVariableArgumentHandler environmentVariableService = services.GetRequiredService<IEnvironmentVariableArgumentHandler>();
        if (!environmentVariableService.TryProcessArgument(environmentVariables, globalEnvironment))
        {
            return 1;
        }

        MetricsOptions metricsOptions = configuration.Get("cyborg.services.metrics", () => new MetricsOptions());

        services.GetRequiredService<MetricsCollectorOptions>().Namespace = metricsOptions.Namespace;
        IModuleConfigurationLoader moduleLoader = services.GetService<IModuleConfigurationLoader>();
        ModuleContext module = await moduleLoader.LoadModuleAsync(main, cancellationToken);
        module = module with
        {
            Environment = module.Environment ?? ModuleEnvironment.Default,
        };
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        string target = globalEnvironment.ResolveVariableOrDefault("target", "<unspecified>");
        logger.LogRunStarted(target);
        IModuleExecutionResult result = await runtime.ExecuteAsync(module, cancellationToken);
        if (result.Status is ModuleExitStatus.Success or ModuleExitStatus.Skipped)
        {
            logger.LogRunCompleted(target);
        }
        else
        {
            logger.LogRunCompletedWithStatus(target, result.Status.ToString());
            if (!(configuration.TryGetValue("cyborg.services.logging.console:enabled", out bool enabled) && enabled)
                && configuration.TryGetValue("cyborg.services.logging.file:enabled", out enabled) && enabled)
            {
                string logFile = configuration.Get("cyborg.services.logging.file:path", defaultValue: "/var/log/cyborg/latest.log");
                await using Stream logStream = File.OpenRead(logFile);
                using Stream stdout = Console.OpenStandardOutput();
                await logStream.CopyToAsync(stdout, cancellationToken);
            }
        }
        IMetricsCollector metricsCollector = services.GetRequiredService<IMetricsCollector>();
        string metricsDestinationPath = metrics ?? metricsOptions.FilePath;
        string tempDestination = $"{metricsDestinationPath}.tmp";
        await using (Stream metricsOutput = File.OpenWrite(tempDestination))
        {
            await metricsCollector.WriteToAsync(metricsOutput, cancellationToken);
        }
        File.Move(tempDestination, metricsDestinationPath, overwrite: true);
        return result.Status == ModuleExitStatus.Success ? 0 : 2;
    }
}
