using ConsoleAppFramework;
using Cyborg.Cli.Arguments;
using Cyborg.Cli.Configuration;
using Cyborg.Cli.Debugging;
using Cyborg.Cli.Logging;
using Cyborg.Cli.Metrics;
using Cyborg.Core.Configuration;
using Cyborg.Core.Configuration.Builders;
using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Extensions;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Services.Metrics;
using Cyborg.Core.Text;
using Cyborg.Core.Text.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Cli;

[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Must be instance method for ConsoleAppFramework.")]
internal sealed class Commands
{
    private const string CYBORG_ROOT = "/etc/cyborg";
    private const string LAST_RUN_SUCCESS = "last_run_success";

    /// <summary>
    /// Executes a backup run using the provided configuration and command-line options.
    /// </summary>
    /// <remarks>
    /// This method loads configuration, sets up the runtime environment, executes the configured main module, and writes metrics output.
    /// Logging and metrics behavior can be customized through command-line arguments and host configuration.
    /// If the run fails and file logging is enabled, the log file is written to standard output.
    /// When <paramref name="breakAt"/> is supplied, workflow execution pauses at matching modules and opens an interactive debug REPL.
    /// </remarks>
    /// <param name="main">The file path to the main module configuration. Defaults to the primary configuration file if not specified.</param>
    /// <param name="options">The file path to the options configuration. Defaults to the standard options file if not specified.</param>
    /// <param name="environmentVariables">
    /// -e, An optional array of environment variable assignments to inject into the global environment. Each element must use `key[:type]=value`.
    /// The optional type must identify a supported dynamic value provider.
    /// If no type is specified, the value is treated as a literal string. When a type is specified, the value must be a valid JSON literal for the selected provider.
    /// </param>
    /// <param name="config">
    /// -c, Optional host configuration overrides. Each element must use `key[:type]=value`. Untyped values are strings;
    /// typed values are parsed as JSON through the dynamic value provider registry. Configuration hierarchy uses dot-delimited keys,
    /// leaving the optional single-colon suffix exclusively for the dynamic value type annotation.
    /// Multiple definitions use array input; JSON-array syntax preserves definitions whose values contain commas.
    /// </param>
    /// <param name="metrics">The file path where metrics output will be written. If null, the default metrics file path from configuration is used.</param>
    /// <param name="logLevel">The minimum log level to use for console output. If null, the default log level from configuration is used.</param>
    /// <param name="breakAt">
    /// Optional module id, name, or group regular expressions. Execution breaks after the matching module has been prepared and its constraints evaluated,
    /// but before validation is enforced and before its worker runs. Repeat the flag to register multiple breakpoints.
    /// </param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation and yields the process exit code.</returns>
    [Command("run")]
    public async Task<int> RunAsync(
        string main = $"{CYBORG_ROOT}/cyborg.jconf",
        string options = $"{CYBORG_ROOT}/cyborg.options.jconf",
        string[]? environmentVariables = null,
        string[]? config = null,
        string? metrics = null,
        LogLevel? logLevel = null,
        string[]? breakAt = null,
        CancellationToken cancellationToken = default)
    {
        using DefaultServiceProvider services = new();
        IConfiguration configuration = services.GetRequiredService<IConfiguration>();
        IConfigurationBuilder configurationBuilder = services.GetRequiredService<IConfigurationBuilder>();
        ICliConfigurationService cliConfigurationService = services.GetRequiredService<ICliConfigurationService>();
        bool configurationArgumentsValid = cliConfigurationService.TryConfigure(
            configurationBuilder,
            options,
            config,
            out string? configurationArgumentError);
        ICliDebugArgumentHandler debugArgumentHandler = services.GetRequiredService<ICliDebugArgumentHandler>();
        bool debuggerArgumentsValid = debugArgumentHandler.TryConfigure(breakAt, out string? invalidBreakpointExpression, out string? debuggerArgumentError);
        await configurationBuilder.ApplyToAsync(configuration, cancellationToken);

        MetricsOptions metricsDefaults = CliConfigurationDefaults.Metrics;
        string metricsNamespace = configuration.Get(CliConfigurationDefaults.METRICS_NAMESPACE_KEY, metricsDefaults.Namespace);
        string configuredMetricsPath = configuration.Get(CliConfigurationDefaults.METRICS_FILE_PATH_KEY, metricsDefaults.FilePath);
        services.GetRequiredService<MetricsCollectorOptions>().Namespace = metricsNamespace;
        IMetricsCollector metricsCollector = services.GetRequiredService<IMetricsCollector>();
        string metricsDestinationPath = metrics ?? configuredMetricsPath;
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IRuntimeEnvironment globalEnvironment = runtime.GlobalEnvironment;
        bool runSucceeded = false;

        try
        {
            // CLI --log-level overrides only the console sink minimum level.
            if (logLevel.HasValue)
            {
                services.GetRequiredService<LoggingOptions>().MinimumLevel = logLevel.Value;
            }

            ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("cyborg.cli.main");

            if (!configurationArgumentsValid)
            {
                logger.LogInvalidConfigurationOverride(configurationArgumentError!);
                return 1;
            }
            if (!debuggerArgumentsValid)
            {
                logger.LogInvalidBreakAtExpression(invalidBreakpointExpression!, debuggerArgumentError!);
                return 1;
            }
            if (!debugArgumentHandler.HasUsableFrontend())
            {
                logger.LogBreakAtWithoutDebugFrontend(debugArgumentHandler.FrontendConfigurationKey);
                return 1;
            }

            IEnvironmentVariableArgumentHandler environmentVariableService = services.GetRequiredService<IEnvironmentVariableArgumentHandler>();
            if (!environmentVariableService.TryProcessArgument(environmentVariables, globalEnvironment))
            {
                return 1;
            }

            DynamicArgumentLogRenderer argumentLogRenderer = services.GetRequiredService<DynamicArgumentLogRenderer>();
            logger.LogStartup(RenderRunArguments(main, options, environmentVariables, config, metrics, logLevel, breakAt, argumentLogRenderer));

            IModuleConfigurationLoader moduleLoader = services.GetRequiredService<IModuleConfigurationLoader>();
            ModuleContext module = await moduleLoader.LoadModuleAsync(main, cancellationToken);
            module = module with
            {
                Environment = module.Environment ?? ModuleEnvironment.Default,
            };
            TaggedString target = globalEnvironment.ResolveVariableOrDefault(WellKnownVariables.Target, new TaggedString("<unspecified>"));
            string renderedTarget = services.GetRequiredService<ITaggedStringRenderer>().Render(target);
            logger.LogRunStarted(renderedTarget);
            IModuleExecutionResult result = await runtime.ExecuteAsync(module, cancellationToken);
            if (result.Status is ModuleExitStatus.Success or ModuleExitStatus.Skipped)
            {
                logger.LogRunCompleted(renderedTarget);
            }
            else
            {
                logger.LogRunCompletedWithStatus(renderedTarget, result.Status.ToString());
                if (!(configuration.TryGetValue(CliConfigurationDefaults.CONSOLE_LOGGING_ENABLED_KEY, out bool enabled) && enabled)
                    && configuration.TryGetValue(CliConfigurationDefaults.FILE_LOGGING_ENABLED_KEY, out enabled) && enabled)
                {
                    string logFile = configuration.Get(CliConfigurationDefaults.FILE_LOGGING_PATH_KEY, CliConfigurationDefaults.FileLogging.Path);
                    await using Stream logStream = File.OpenRead(logFile);
                    using Stream stdout = Console.OpenStandardOutput();
                    await logStream.CopyToAsync(stdout, cancellationToken);
                }
            }

            runSucceeded = result.Status == ModuleExitStatus.Success;
            return runSucceeded ? 0 : 2;
        }
        finally
        {
            CollectRunMetrics(globalEnvironment, metricsCollector, runSucceeded);
            await WriteMetricsAsync(metricsCollector, metricsDestinationPath, CancellationToken.None);
        }
    }

    private static string RenderRunArguments(
        string main,
        string options,
        string[]? environmentVariables,
        string[]? config,
        string? metrics,
        LogLevel? logLevel,
        string[]? breakAt,
        DynamicArgumentLogRenderer dynamicArgumentLogRenderer)
    {
        return string.Join(", ",
        [
            $"main={QuoteArgument(main)}",
            $"options={QuoteArgument(options)}",
            $"environmentVariables={RenderArgumentArray(environmentVariables, dynamicArgumentLogRenderer.RenderDefinition)}",
            $"config={RenderArgumentArray(config, dynamicArgumentLogRenderer.RenderDefinition)}",
            $"metrics={RenderOptionalArgument(metrics)}",
            $"logLevel={(logLevel.HasValue ? logLevel.Value.ToString() : "null")}",
            $"breakAt={RenderArgumentArray(breakAt)}",
        ]);
    }

    private static string RenderArgumentArray(string[]? values, Func<string, string>? renderer = null)
    {
        if (values is null)
        {
            return "null";
        }

        List<string> renderedValues = [];
        foreach (string value in values)
        {
            string renderedValue = renderer is null ? value : renderer(value);
            renderedValues.Add(QuoteArgument(renderedValue));
        }
        return $"[{string.Join(", ", renderedValues)}]";
    }

    private static string RenderOptionalArgument(string? value) => value is null ? "null" : QuoteArgument(value);

    private static string QuoteArgument(string value)
    {
        string escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private static void CollectRunMetrics(IEnvironmentLike environment, IMetricsCollector metricsCollector, bool runSucceeded)
    {
        IMetricsLabelCollection labels = metricsCollector.CreateLabels();
        if (environment.TryResolveVariable(WellKnownVariables.Target, out TaggedString target))
        {
            labels.AddLabel("target", target);
        }
        metricsCollector.AddGauge(LAST_RUN_SUCCESS, "Whether the most recent Cyborg run completed successfully (1 for success, 0 for failure)", samples => samples
            .Add(runSucceeded ? 1 : 0, labels));
    }

    private static async Task WriteMetricsAsync(IMetricsCollector metricsCollector, string metricsDestinationPath, CancellationToken cancellationToken)
    {
        string tempDestination = $"{metricsDestinationPath}.tmp";
        await using (Stream metricsOutput = File.Create(tempDestination))
        {
            await metricsCollector.WriteToAsync(metricsOutput, cancellationToken);
        }
        File.Move(tempDestination, metricsDestinationPath, overwrite: true);
    }
}

file static class WellKnownVariables
{
    public static string Target => "target";
}
