using ConsoleAppFramework;
using Cyborg.Cli.Logging;
using Cyborg.Cli.Metrics;
using Cyborg.Core.Configuration;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Services.Metrics;
using Cyborg.Modules.Borg;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Cli;

[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Must be instance method for ConsoleAppFramework.")]
internal sealed class Commands
{
    private const string CYBORG_ROOT = "/etc/cyborg";

    /// <summary>
    /// Executes a backup run for the specified target using the provided configuration and options.
    /// </summary>
    /// <remarks>
    /// This method loads configuration, sets up the runtime environment, executes the specified backup module, and writes metrics output. Logging and metrics behavior can be customized via parameters or configuration files. If the run fails and file logging is enabled, the log file is written to standard output.
    /// </remarks>
    /// <param name="target">The name of the backup target to execute. This value is used to select the configuration and environment for the run.</param>
    /// <param name="dryRun">true to perform a dry run without making changes; otherwise, false. When set to true, actions are simulated but not executed.</param>
    /// <param name="mainModulePath">The file path to the main module configuration. Defaults to the primary configuration file if not specified.</param>
    /// <param name="optionsPath">The file path to the options configuration. Defaults to the standard options file if not specified.</param>
    /// <param name="metricsOutputPath">The file path where metrics output will be written. If null, the default metrics file path from configuration is used.</param>
    /// <param name="logLevel">The minimum log level to use for console output. If null, the default log level from configuration is used.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Command("run")]
    public async Task RunAsync([Argument] string target,
        bool dryRun = false,
        string mainModulePath = $"{CYBORG_ROOT}/cyborg.jconf",
        string optionsPath = $"{CYBORG_ROOT}/cyborg.options.jconf",
        string? metricsOutputPath = null,
        LogLevel? logLevel = null,
        CancellationToken cancellationToken = default)
    {
        using DefaultServiceProvider services = new();
        IConfiguration configuration = services.GetRequiredService<IConfiguration>();
        IConfigurationLoader configurationLoader = services.GetRequiredService<IConfigurationLoader>();
        await configurationLoader.AddSourceAsync(configuration, optionsPath, cancellationToken);

        // CLI --log-level overrides only the console sink minimum level.
        if (logLevel.HasValue)
        {
            services.GetRequiredService<LoggingOptions>().MinimumLevel = logLevel.Value;
        }

        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("cyborg.cli.main");
        GlobalRuntimeEnvironment globalEnvironment = services.GetRequiredService<GlobalRuntimeEnvironment>();
        globalEnvironment.SetVariable("target", target);
        if (dryRun)
        {
            globalEnvironment.SetVariable(BorgWellKnownVariables.DRY_RUN, true);
        }
        MetricsOptions metricsOptions = configuration.Get("cyborg.services.metrics", () => new MetricsOptions());

        services.GetRequiredService<MetricsCollectorOptions>().Namespace = metricsOptions.Namespace;
        IModuleConfigurationLoader moduleLoader = services.GetService<IModuleConfigurationLoader>();
        ModuleContext module = await moduleLoader.LoadModuleAsync(mainModulePath, cancellationToken);
        module = module with 
        {
            Environment = module.Environment ?? ModuleEnvironment.Default,
        };
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
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
        IMetricsCollector metrics = services.GetRequiredService<IMetricsCollector>();
        string metricsDestinationPath = metricsOutputPath ?? metricsOptions.FilePath;
        string tempDestination = $"{metricsDestinationPath}.tmp";
        await using (Stream metricsOutput = File.OpenWrite(tempDestination))
        {
            await metrics.WriteToAsync(metricsOutput, cancellationToken);
        }
        File.Move(tempDestination, metricsDestinationPath, overwrite: true);
    }
}
