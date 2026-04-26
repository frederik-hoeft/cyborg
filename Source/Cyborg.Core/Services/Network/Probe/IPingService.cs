namespace Cyborg.Core.Services.Network.Probe;

public interface IPingService
{
    Task<bool> PingAsync(string host, TimeSpan timeout, CancellationToken cancellationToken);
}
