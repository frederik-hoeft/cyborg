using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Modules.Retry;

internal static partial class RetryModuleWorkerLog
{
    [ZLoggerMessage(LogLevel.Debug, "Executing retry attempt {attempt} of {attempts}")]
    public static partial void LogRetryAttempt(this ILogger logger, int attempt, int attempts);

    [ZLoggerMessage(LogLevel.Debug, "Retry attempt {attempt} of {attempts} succeeded")]
    public static partial void LogRetrySucceeded(this ILogger logger, int attempt, int attempts);

    [ZLoggerMessage(LogLevel.Debug, "Retry attempt {attempt} of {attempts} finished with status '{status}'")]
    public static partial void LogRetryAttemptFailed(this ILogger logger, int attempt, int attempts, string status);

    [ZLoggerMessage(LogLevel.Error, "Retry exhausted {attempts} attempts")]
    public static partial void LogRetryExhausted(this ILogger logger, int attempts);

    [ZLoggerMessage(LogLevel.Debug, "Retry canceled on attempt {attempt} of {attempts}")]
    public static partial void LogRetryCanceled(this ILogger logger, int attempt, int attempts);
}
