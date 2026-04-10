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
        logger.LogStartup(string.Join(' ', Environment.GetCommandLineArgs()[1..]));
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
