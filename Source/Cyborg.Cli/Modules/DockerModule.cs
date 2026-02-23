using Cyborg.Core.Configuration;
using Cyborg.Core.Execution;
using Cyborg.Core.Logging;

namespace Cyborg.Cli.Modules;

public sealed class DockerModule : ModuleBase
{
    private readonly ProcessExecutor _executor;
    private readonly DockerConfiguration _config;
    private bool _containersStopped;

    public override string Name => "Docker";

    public DockerModule(
        ILogger logger,
        ProcessExecutor executor,
        DockerConfiguration config) : base(logger)
    {
        _executor = executor;
        _config = config;
    }

    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await base.InitializeAsync(cancellationToken);
        await StopContainersAsync(cancellationToken);
    }

    public override Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        // Docker module primarily handles initialization and cleanup
        return Task.CompletedTask;
    }

    private async Task StopContainersAsync(CancellationToken cancellationToken)
    {
        Logger.Info($"Attempting to stop '{_config.ContainerGroup}' docker container group...");

        if (!string.IsNullOrEmpty(_config.User))
        {
            var result = await _executor.ExecuteAsync(
                "/usr/bin/su",
                $"- {_config.User} -c \"{_config.ComposePath} down\"",
                cancellationToken: cancellationToken);
            
            if (!result.Success)
            {
                throw new InvalidOperationException($"Failed to stop containers: {result.StandardError}");
            }
        }
        else
        {
            var result = await _executor.ExecuteAsync(_config.ComposePath, "down", cancellationToken: cancellationToken);
            
            if (!result.Success)
            {
                throw new InvalidOperationException($"Failed to stop containers: {result.StandardError}");
            }
        }

        _containersStopped = true;
    }

    public override async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        if (!_containersStopped)
        {
            await base.CleanupAsync(cancellationToken);
            return;
        }

        Logger.Info($"Attempting to start '{_config.ContainerGroup}' docker container group...");

        if (!string.IsNullOrEmpty(_config.User))
        {
            var result = await _executor.ExecuteAsync(
                "/usr/bin/su",
                $"- {_config.User} -c \"{_config.ComposePath} up -d\"",
                cancellationToken: cancellationToken);
            
            if (result.Success)
            {
                Logger.Info($"Successfully started '{_config.ContainerGroup}' docker container group");
            }
            else
            {
                Logger.Error($"Failed to start containers: {result.StandardError}");
            }
        }
        else
        {
            var result = await _executor.ExecuteAsync(_config.ComposePath, "up -d", cancellationToken: cancellationToken);
            
            if (result.Success)
            {
                Logger.Info($"Successfully started '{_config.ContainerGroup}' docker container group");
            }
            else
            {
                Logger.Error($"Failed to start containers: {result.StandardError}");
            }
        }

        await base.CleanupAsync(cancellationToken);
    }
}
