using System.Net.Sockets;

namespace Cyborg.Core.Services.Network.Probe;

public sealed class TcpPortProbeService : IPortProbeService
{
    public PortProbeProtocol Protocol => PortProbeProtocol.Tcp;

    public async Task<bool> ProbePortAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using TcpClient tcpClient = new();
        DateTime startTime = DateTime.UtcNow;
        while (!cancellationToken.IsCancellationRequested && startTime.Add(timeout) > DateTime.UtcNow)
        {
            try
            {
                await tcpClient.ConnectAsync(host, port, cancellationToken);
                return true;
            }
            catch (SocketException)
            {
                // Port is closed or host is unreachable, wait and retry
                await Task.Delay(500, cancellationToken);
            }
        }
        return false;
    }
}
