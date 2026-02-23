using Cyborg.Core.Logging;

namespace Cyborg.Cli.Modules;

public abstract class ModuleBase : IModule
{
    protected ILogger Logger { get; }

    public abstract string Name { get; }

    protected ModuleBase(ILogger logger)
    {
        Logger = logger;
    }

    public virtual Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Logger.Info($"{Name} module initializing...");
        return Task.CompletedTask;
    }

    public abstract Task ExecuteAsync(CancellationToken cancellationToken = default);

    public virtual Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        Logger.Info($"{Name} module cleanup completed");
        return Task.CompletedTask;
    }
}
