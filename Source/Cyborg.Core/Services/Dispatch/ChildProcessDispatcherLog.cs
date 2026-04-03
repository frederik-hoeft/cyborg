using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Core.Services.Dispatch;

internal static partial class ChildProcessDispatcherLog
{
    [ZLoggerMessage(LogLevel.Debug, "Launching process: {executable} {arguments}")]
    public static partial void LogProcessLaunching(this ILogger logger, string executable, string arguments);

    [ZLoggerMessage(LogLevel.Information, "Process started: {executable}")]
    public static partial void LogProcessStarted(this ILogger logger, string executable);

    [ZLoggerMessage(LogLevel.Error, "Failed to start process: {executable}")]
    public static partial void LogProcessStartFailed(this ILogger logger, string executable, Exception exception);

    [ZLoggerMessage(LogLevel.Information, "Process {executable} exited with code {exitCode}")]
    public static partial void LogProcessExited(this ILogger logger, string executable, int exitCode);

    [ZLoggerMessage(LogLevel.Trace, "Process {executable} stdout: {standardOutput}")]
    public static partial void LogProcessStandardOutput(this ILogger logger, string executable, string standardOutput);

    [ZLoggerMessage(LogLevel.Trace, "Process {executable} stderr: {standardError}")]
    public static partial void LogProcessStandardError(this ILogger logger, string executable, string standardError);

    [ZLoggerMessage(LogLevel.Warning, "Process {executable} was killed after cancellation was requested")]
    public static partial void LogProcessKilled(this ILogger logger, string executable);
}
