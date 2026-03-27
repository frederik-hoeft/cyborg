using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Modules.Logging;

internal static partial class ModuleLog
{
    // Subprocess lifecycle events

    [ZLoggerMessage(LogLevel.Information, "Executing subprocess: {executable} ({argumentCount} argument(s))")]
    public static partial void LogSubprocessStarted(this ILogger logger, string executable, int argumentCount);

    [ZLoggerMessage(LogLevel.Information, "Subprocess {executable} exited with code {exitCode}")]
    public static partial void LogSubprocessCompleted(this ILogger logger, string executable, int exitCode);

    [ZLoggerMessage(LogLevel.Warning, "Subprocess {executable} failed with exit code {exitCode}")]
    public static partial void LogSubprocessFailed(this ILogger logger, string executable, int exitCode);

    // Sequence lifecycle events

    [ZLoggerMessage(LogLevel.Debug, "Executing sequence step {stepIndex} of {totalSteps}")]
    public static partial void LogSequenceStep(this ILogger logger, int stepIndex, int totalSteps);

    [ZLoggerMessage(LogLevel.Information, "Sequence completed with status: {status}")]
    public static partial void LogSequenceCompleted(this ILogger logger, string status);

    // General module lifecycle events

    [ZLoggerMessage(LogLevel.Debug, "Module {moduleId} execution started")]
    public static partial void LogModuleStarted(this ILogger logger, string moduleId);

    [ZLoggerMessage(LogLevel.Debug, "Module {moduleId} execution completed with status: {status}")]
    public static partial void LogModuleCompleted(this ILogger logger, string moduleId, string status);

    [ZLoggerMessage(LogLevel.Warning, "Module {moduleId} execution was canceled")]
    public static partial void LogModuleCanceled(this ILogger logger, string moduleId);
}
