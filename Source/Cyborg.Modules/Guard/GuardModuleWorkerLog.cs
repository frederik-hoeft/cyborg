using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Modules.Guard;

internal static partial class GuardModuleWorkerLog
{
    [ZLoggerMessage(LogLevel.Debug, "Executing guard '{moduleId}' try block")]
    public static partial void LogGuardTryExecuting(this ILogger logger, string moduleId);

    [ZLoggerMessage(LogLevel.Warning, "Guard '{moduleId}' try block failed with status '{status}' — executing catch block")]
    public static partial void LogGuardCatchTriggeredByFailure(this ILogger logger, string moduleId, string status);

    [ZLoggerMessage(LogLevel.Warning, "Guard '{moduleId}' try block raised an exception ({exceptionType}) — executing catch block")]
    public static partial void LogGuardCatchTriggeredByException(this ILogger logger, string moduleId, string exceptionType);

    [ZLoggerMessage(LogLevel.Warning, "Guard '{moduleId}' has no explicit catch block; applying behavior '{behavior}'")]
    public static partial void LogGuardNoCatchBlock(this ILogger logger, string moduleId, string behavior);

    [ZLoggerMessage(LogLevel.Debug, "Executing guard '{moduleId}' finally block")]
    public static partial void LogGuardFinallyExecuting(this ILogger logger, string moduleId);

    [ZLoggerMessage(LogLevel.Warning, "Guard '{moduleId}' finally block skipped due to cancellation")]
    public static partial void LogGuardFinallySkipped(this ILogger logger, string moduleId);

    [ZLoggerMessage(LogLevel.Debug, "Guard '{moduleId}' completed with status '{status}'")]
    public static partial void LogGuardCompleted(this ILogger logger, string moduleId, string status);
}
