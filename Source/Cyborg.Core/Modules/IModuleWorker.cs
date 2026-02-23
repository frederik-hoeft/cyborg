namespace Cyborg.Core.Modules;

public interface IModuleWorker
{
    string ModuleId { get; }

    Task<bool> ExecuteAsync(CancellationToken cancellationToken);
}
