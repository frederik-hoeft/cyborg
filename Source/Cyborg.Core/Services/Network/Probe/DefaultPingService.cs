using System.Net.NetworkInformation;

namespace Cyborg.Core.Services.Network.Probe;

public sealed class DefaultPingService : IPingService, IDisposable
{
    private readonly Ping _ping = new();

    public async Task<bool> PingAsync(string host, TimeSpan timeout, CancellationToken cancellationToken)
    {
        PingReply reply = await _ping.SendPingAsync(host, timeout, cancellationToken: cancellationToken);
        return reply.Status == IPStatus.Success;
    }

    public void Dispose() => _ping.Dispose();
}