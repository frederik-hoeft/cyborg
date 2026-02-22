using Cyborg.Core.Configuration;
using Cyborg.Core.Execution;
using Cyborg.Core.Logging;

namespace Cyborg.Core.Modules;

public sealed class BackupOrchestrator
{
    private readonly ILogger _logger;
    private readonly ProcessExecutor _executor;
    private readonly BackupJobConfiguration _jobConfig;
    private readonly List<IModule> _modules = new();

    public BackupOrchestrator(
        ILogger logger,
        ProcessExecutor executor,
        BackupJobConfiguration jobConfig)
    {
        _logger = logger;
        _executor = executor;
        _jobConfig = jobConfig;

        InitializeModules();
    }

    private void InitializeModules()
    {
        // Wake-on-LAN module (if configured)
        if (_jobConfig.WakeOnLan != null)
        {
            _modules.Add(new WakeOnLanModule(
                _logger,
                _executor,
                _jobConfig.Hosts,
                _jobConfig.WakeOnLan));
        }

        // Docker module (if configured)
        if (_jobConfig.Docker != null)
        {
            _modules.Add(new DockerModule(
                _logger,
                _executor,
                _jobConfig.Docker));
        }

        // Borg backup module (always present)
        _modules.Add(new BorgModule(
            _logger,
            _executor,
            _jobConfig.Hosts,
            _jobConfig.Borg,
            _jobConfig.Passphrase));
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.Info($"[START] {_jobConfig.Name} is initializing...");

        try
        {
            // Initialize all modules
            foreach (var module in _modules)
            {
                await module.InitializeAsync(cancellationToken);
            }

            _logger.Info("Ok. Proceeding with backup...");

            // Execute all modules
            foreach (var module in _modules)
            {
                await module.ExecuteAsync(cancellationToken);
            }

            // Cleanup all modules (in reverse order)
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                await _modules[i].CleanupAsync(cancellationToken);
            }

            _logger.Info($"[SUCCESS] {_jobConfig.Name} completed successfully");
        }
        catch (Exception ex)
        {
            _logger.Error($"[FAILED] {_jobConfig.Name} failed: {ex.Message}");

            // Attempt cleanup even on failure (in reverse order)
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                try
                {
                    await _modules[i].CleanupAsync(cancellationToken);
                }
                catch (Exception cleanupEx)
                {
                    _logger.Error($"Cleanup failed for {_modules[i].Name}: {cleanupEx.Message}");
                }
            }

            throw;
        }
    }
}
