using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Cyborg.Core.Configuration;
using Cyborg.Core.Execution;
using Cyborg.Core.Logging;

namespace Cyborg.Core.Modules;

public sealed class WakeOnLanModule : ModuleBase
{
    private readonly ProcessExecutor _executor;
    private readonly List<BackupHostConfiguration> _hosts;
    private readonly WakeOnLanConfiguration _config;
    private readonly List<string> _wokenHosts = new();

    public override string Name => "WakeOnLan";

    public WakeOnLanModule(
        ILogger logger,
        ProcessExecutor executor,
        List<BackupHostConfiguration> hosts,
        WakeOnLanConfiguration config) : base(logger)
    {
        _executor = executor;
        _hosts = hosts;
        _config = config;
    }

    public override async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            Logger.Info("Wake-on-LAN is disabled");
            return;
        }

        Logger.Info($"Waking up backup hosts...");
        Logger.Info($"foreach_backup_host: processing {_hosts.Count} backup hosts");

        foreach (var host in _hosts)
        {
            Logger.Info($"foreach_backup_host: executing command for host {host.Hostname}:{host.Port}");
            await WakeHostIfNeededAsync(host, cancellationToken);
        }

        Logger.Info($"foreach_backup_host: completed successfully for all {_hosts.Count} hosts");
    }

    private async Task WakeHostIfNeededAsync(BackupHostConfiguration host, CancellationToken cancellationToken)
    {
        Logger.Info($"Checking if {host.Hostname} is up...");

        if (await IsHostReachableAsync(host, cancellationToken))
        {
            Logger.Info($"{host.Hostname} is reachable on port {host.Port}.");
            return;
        }

        Logger.Warn($"{host.Hostname} is unreachable! Attempting Wake on LAN.");

        if (string.IsNullOrEmpty(host.MacAddress))
        {
            Logger.Error($"No MAC address configured for {host.Hostname}");
            throw new InvalidOperationException($"Cannot wake {host.Hostname}: no MAC address configured");
        }

        var ipAddress = await ResolveHostnameAsync(host.Hostname);
        Logger.Info($"DNS reports {host.Hostname} at {ipAddress}");

        await SendWakeOnLanPacketAsync(ipAddress, host.MacAddress);

        // Wait for host to wake up
        for (int i = 0; i < _config.Retries; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(_config.Timeout / _config.Retries), cancellationToken);
            
            if (await IsHostReachableAsync(host, cancellationToken))
            {
                Logger.Info($"{host.Hostname} is now awake!");
                Logger.Info($"{host.Hostname} is reachable on port {host.Port}.");
                _wokenHosts.Add(host.Hostname);
                return;
            }
        }

        throw new TimeoutException($"Failed to wake {host.Hostname} after {_config.Timeout} seconds");
    }

    private async Task<bool> IsHostReachableAsync(BackupHostConfiguration host, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host.Hostname, host.Port, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> ResolveHostnameAsync(string hostname)
    {
        var addresses = await Dns.GetHostAddressesAsync(hostname);
        var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
        return ipv4?.ToString() ?? addresses[0].ToString();
    }

    private async Task SendWakeOnLanPacketAsync(string ipAddress, string macAddress)
    {
        var result = await _executor.ExecuteAsync(
            "/usr/bin/wakeonlan",
            $"-i {ipAddress} {macAddress.Replace(":", "")}");

        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to send WOL packet: {result.StandardError}");
        }
    }

    public override async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        if (_wokenHosts.Count == 0)
        {
            await base.CleanupAsync(cancellationToken);
            return;
        }

        Logger.Info($"Shutting down {_wokenHosts.Count} hosts that were woken up");
        
        foreach (var hostname in _wokenHosts)
        {
            Logger.Info($"Shutting down {hostname}...");
            // Note: Actual shutdown would be implemented here
            // This would typically use SSH to send a shutdown command
        }

        await base.CleanupAsync(cancellationToken);
    }
}
