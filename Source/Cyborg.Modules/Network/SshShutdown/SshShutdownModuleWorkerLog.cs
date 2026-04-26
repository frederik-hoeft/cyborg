using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Modules.Network.SshShutdown;

internal static partial class SshShutdownModuleWorkerLog
{
    [ZLoggerMessage(LogLevel.Debug, "Sending SSH shutdown command to '{hostname}'")]
    public static partial void LogSshShutdownSending(this ILogger logger, string hostname);

    [ZLoggerMessage(LogLevel.Information, "SSH shutdown command completed successfully on '{hostname}'")]
    public static partial void LogSshShutdownSucceeded(this ILogger logger, string hostname);

    [ZLoggerMessage(LogLevel.Warning, "SSH shutdown command failed on '{hostname}' with exit code {exitCode}")]
    public static partial void LogSshShutdownFailed(this ILogger logger, string hostname, int exitCode);
}
