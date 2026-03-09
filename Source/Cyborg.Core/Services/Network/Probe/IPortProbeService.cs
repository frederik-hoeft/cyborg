namespace Cyborg.Core.Services.Network.Probe;

public interface IPortProbeService
{
    PortProbeProtocol Protocol { get; }

    Task<bool> ProbePortAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default);
}