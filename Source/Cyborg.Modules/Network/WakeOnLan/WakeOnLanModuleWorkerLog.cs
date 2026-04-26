using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Modules.Network.WakeOnLan;

internal static partial class WakeOnLanModuleWorkerLog
{
    [ZLoggerMessage(LogLevel.Debug, "Checking if '{targetHost}' is already reachable with discovery timeout of {discoveryTimeout}")]
    public static partial void LogWolCheckingReachability(this ILogger logger, string targetHost, string discoveryTimeout);

    [ZLoggerMessage(LogLevel.Information, "Host '{targetHost}' is already reachable — no wake needed")]
    public static partial void LogWolHostAlreadyUp(this ILogger logger, string targetHost);

    [ZLoggerMessage(LogLevel.Debug, "Host '{targetHost}' is unreachable — sending Wake-on-LAN packet to '{macAddress}'")]
    public static partial void LogWolSendingPacket(this ILogger logger, string targetHost, string macAddress);

    [ZLoggerMessage(LogLevel.Error, "Wake-on-LAN command failed with exit code {exitCode}: {standardError}")]
    public static partial void LogWolCommandFailed(this ILogger logger, int exitCode, string? standardError);

    [ZLoggerMessage(LogLevel.Debug, "Probing host '{targetHost}' on port {port} for up to {maxWaitTime}")]
    public static partial void LogWolProbingHost(this ILogger logger, string targetHost, int port, string maxWaitTime);

    [ZLoggerMessage(LogLevel.Information, "Host '{targetHost}' came online after Wake-on-LAN")]
    public static partial void LogWolHostCameOnline(this ILogger logger, string targetHost);

    [ZLoggerMessage(LogLevel.Warning, "Host '{targetHost}' did not come online within the timeout")]
    public static partial void LogWolHostTimeout(this ILogger logger, string targetHost);
}
