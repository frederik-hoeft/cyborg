namespace Cyborg.Core.Services.Network;

public interface IPingService
{
    Task<bool> PingAsync(string host, TimeSpan timeout, CancellationToken cancellationToken);
}