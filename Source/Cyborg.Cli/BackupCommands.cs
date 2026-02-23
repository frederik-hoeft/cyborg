using ConsoleAppFramework;
using Cyborg.Core;
using Cyborg.Core.Execution;
using Cyborg.Core.Logging;
using Cyborg.Core.Modules;
using System.Text.Json;

namespace Cyborg.Cli;

public class BackupCommands
{
    /// <summary>
    /// Runs a backup job from the specified configuration file
    /// </summary>
    /// <param name="config">Path to the backup job configuration JSON file</param>
    /// <param name="interactive">Run in interactive mode (log to console)</param>
    [Command("run")]
    public async Task RunBackupAsync(
        [Argument] string config,
        bool interactive = false)
    {
        // Load configuration
        var configJson = await File.ReadAllTextAsync(config);
        var jobConfig = JsonSerializer.Deserialize(configJson, CyborgJsonContext.Default.BackupJobConfiguration);

        if (jobConfig == null)
        {
            Console.Error.WriteLine($"Failed to load configuration from {config}");
            Environment.Exit(1);
            return;
        }

        // Set up logging
        var sinks = new List<ILogSink>
        {
            new JsonLogSink(Console.Out)
        };

        var logger = new Logger(sinks);
        var executor = new ProcessExecutor(logger);
        var orchestrator = new BackupOrchestrator(logger, executor, jobConfig);

        try
        {
            await orchestrator.RunAsync();
        }
        catch (Exception ex)
        {
            logger.Error($"Backup job failed: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Validates a backup job configuration file
    /// </summary>
    /// <param name="config">Path to the backup job configuration JSON file</param>
    [Command("validate")]
    public async Task ValidateConfigAsync([Argument] string config)
    {
        try
        {
            var configJson = await File.ReadAllTextAsync(config);
            var jobConfig = JsonSerializer.Deserialize(configJson, CyborgJsonContext.Default.BackupJobConfiguration);

            if (jobConfig == null)
            {
                Console.Error.WriteLine($"Failed to parse configuration file: {config}");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"Configuration file '{config}' is valid");
            Console.WriteLine($"Job name: {jobConfig.Name}");
            Console.WriteLine($"Backup hosts: {jobConfig.Hosts.Count}");
            Console.WriteLine($"Archive name: {jobConfig.Borg.ArchiveName}");
            Console.WriteLine($"Source path: {jobConfig.Borg.SourcePath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Configuration validation failed: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
