namespace Cyborg.Core.Modules;

public interface IModule
{
    string Name { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task ExecuteAsync(CancellationToken cancellationToken = default);
    Task CleanupAsync(CancellationToken cancellationToken = default);
}
