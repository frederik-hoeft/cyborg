using Cyborg.Core.Configuration;
using Cyborg.Core.Execution;
using Cyborg.Core.Logging;

namespace Cyborg.Core.Modules;

public sealed class BorgModule : ModuleBase
{
    private readonly ProcessExecutor _executor;
    private readonly List<BackupHostConfiguration> _hosts;
    private readonly BorgConfiguration _config;
    private readonly string _passphrase;

    public override string Name => "Borg";

    public BorgModule(
        ILogger logger,
        ProcessExecutor executor,
        List<BackupHostConfiguration> hosts,
        BorgConfiguration config,
        string passphrase) : base(logger)
    {
        _executor = executor;
        _hosts = hosts;
        _config = config;
        _passphrase = passphrase;
    }

    public override async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        Logger.Info("Starting backup");
        await CreateBackupsAsync(cancellationToken);

        Logger.Info("Pruning repository");
        await PruneRepositoriesAsync(cancellationToken);

        Logger.Info("Compacting repository");
        await CompactRepositoriesAsync(cancellationToken);
    }

    private async Task CreateBackupsAsync(CancellationToken cancellationToken)
    {
        Logger.Info($"foreach_backup_host: processing {_hosts.Count} backup hosts");

        foreach (var host in _hosts)
        {
            Logger.Info($"foreach_backup_host: executing command for host {host.Hostname}:{host.Port}");
            await CreateBackupAsync(host, cancellationToken);
        }

        Logger.Info($"foreach_backup_host: completed successfully for all {_hosts.Count} hosts");
    }

    private async Task CreateBackupAsync(BackupHostConfiguration host, CancellationToken cancellationToken)
    {
        var repository = $"ssh://borg@{host.Hostname}:{host.Port}{host.Repository}";
        var archiveName = $"::{_config.ArchiveName}";
        
        var excludeArgs = string.Join(" ", _config.ExcludePatterns.Select(p => $"--exclude {p}"));
        var arguments = $"create --show-rc --stats --compression {_config.Compression} --exclude-caches {excludeArgs} {archiveName} {_config.SourcePath}";

        var env = new Dictionary<string, string>
        {
            ["BORG_REPO"] = repository,
            ["BORG_PASSPHRASE"] = _passphrase,
            ["BORG_RSH"] = $"ssh -p {host.Port}"
        };

        var result = await _executor.ExecuteAsync(
            "/usr/bin/borg",
            arguments,
            environmentVariables: env,
            cancellationToken: cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Backup creation failed for {host.Hostname}: {result.StandardError}");
        }
    }

    private async Task PruneRepositoriesAsync(CancellationToken cancellationToken)
    {
        Logger.Info($"foreach_backup_host: processing {_hosts.Count} backup hosts");

        foreach (var host in _hosts)
        {
            Logger.Info($"foreach_backup_host: executing command for host {host.Hostname}:{host.Port}");
            await PruneRepositoryAsync(host, cancellationToken);
        }

        Logger.Info($"foreach_backup_host: completed successfully for all {_hosts.Count} hosts");
    }

    private async Task PruneRepositoryAsync(BackupHostConfiguration host, CancellationToken cancellationToken)
    {
        var repository = $"ssh://borg@{host.Hostname}:{host.Port}{host.Repository}";
        var archivePrefix = _config.ArchiveName.Split('-')[0]; // Extract prefix from archive name template
        
        var arguments = $"prune --list --glob-archives {archivePrefix}-* --show-rc " +
                       $"--keep-daily {_config.KeepDaily} " +
                       $"--keep-weekly {_config.KeepWeekly} " +
                       $"--keep-monthly {_config.KeepMonthly}";

        var env = new Dictionary<string, string>
        {
            ["BORG_REPO"] = repository,
            ["BORG_PASSPHRASE"] = _passphrase,
            ["BORG_RSH"] = $"ssh -p {host.Port}"
        };

        var result = await _executor.ExecuteAsync(
            "/usr/bin/borg",
            arguments,
            environmentVariables: env,
            cancellationToken: cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Repository pruning failed for {host.Hostname}: {result.StandardError}");
        }
    }

    private async Task CompactRepositoriesAsync(CancellationToken cancellationToken)
    {
        Logger.Info($"foreach_backup_host: processing {_hosts.Count} backup hosts");

        foreach (var host in _hosts)
        {
            Logger.Info($"foreach_backup_host: executing command for host {host.Hostname}:{host.Port}");
            await CompactRepositoryAsync(host, cancellationToken);
        }

        Logger.Info($"foreach_backup_host: completed successfully for all {_hosts.Count} hosts");
    }

    private async Task CompactRepositoryAsync(BackupHostConfiguration host, CancellationToken cancellationToken)
    {
        var repository = $"ssh://borg@{host.Hostname}:{host.Port}{host.Repository}";
        var arguments = "compact --show-rc";

        var env = new Dictionary<string, string>
        {
            ["BORG_REPO"] = repository,
            ["BORG_PASSPHRASE"] = _passphrase,
            ["BORG_RSH"] = $"ssh -p {host.Port}"
        };

        var result = await _executor.ExecuteAsync(
            "/usr/bin/borg",
            arguments,
            environmentVariables: env,
            cancellationToken: cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Repository compaction failed for {host.Hostname}: {result.StandardError}");
        }
    }
}
