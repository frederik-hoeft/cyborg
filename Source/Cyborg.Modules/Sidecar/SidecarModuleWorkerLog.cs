using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Modules.Sidecar;

internal static partial class SidecarModuleWorkerLog
{
    [ZLoggerMessage(LogLevel.Debug, "Starting sidecar group with {sidecarCount} sidecar(s)")]
    public static partial void LogSidecarExecutionStarting(this ILogger logger, int sidecarCount);

    [ZLoggerMessage(LogLevel.Debug, "Sidecar primary completed with status '{status}'")]
    public static partial void LogSidecarPrimaryCompleted(this ILogger logger, string status);

    [ZLoggerMessage(LogLevel.Error, "Sidecar {sidecarIndex} failed with status '{status}' — terminating the group")]
    public static partial void LogSidecarFailed(this ILogger logger, int sidecarIndex, string status);

    [ZLoggerMessage(LogLevel.Debug, "Sidecar group completed with primary status '{status}'")]
    public static partial void LogSidecarCompleted(this ILogger logger, string status);
}
