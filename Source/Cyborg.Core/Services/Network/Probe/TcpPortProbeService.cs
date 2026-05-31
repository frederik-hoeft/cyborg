using System.Net.Sockets;

namespace Cyborg.Core.Services.Network.Probe;

public sealed class TcpPortProbeService : IPortProbeService
{
    public PortProbeProtocol Protocol => PortProbeProtocol.Tcp;

    public async Task<bool> ProbePortAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        TimeSpan remaining;

        CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            while (!cancellationToken.IsCancellationRequested && (remaining = deadline.Subtract(DateTimeOffset.UtcNow)) > TimeSpan.Zero)
            {
                if (!timeoutCts.TryReset())
                {
                    timeoutCts.Dispose();
                    timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                }
                try
                {
                    timeoutCts.CancelAfter(remaining);
                    // Recreate the TcpClient on each attempt to avoid issues with reusing an underlying socket that encountered an error
                    // the exact behavior can depend on the platform and .NET implementation, so creating a fresh client is more robust
                    using TcpClient tcpClient = new();
                    await tcpClient.ConnectAsync(host, port, timeoutCts.Token);
                    // Successfully connected, the port is open
                    return true;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return false;
                }
                catch (SocketException)
                {
                    TimeSpan remainingAfterAttempt = deadline.Subtract(DateTimeOffset.UtcNow);
                    if (remainingAfterAttempt <= TimeSpan.Zero)
                    {
                        return false;
                    }
                    TimeSpan delay = TimeSpan.FromSeconds(1);
                    if (delay > remainingAfterAttempt)
                    {
                        delay = remainingAfterAttempt;
                    }
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }
        finally
        {
            timeoutCts.Dispose();
        }
        return false;
    }
}
