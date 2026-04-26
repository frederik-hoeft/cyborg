using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Modules.Foreach;

internal static partial class ForeachModuleWorkerLog
{
    [ZLoggerMessage(LogLevel.Error, "Collection variable '{collectionVariable}' could not be resolved in the current environment")]
    public static partial void LogCollectionNotFound(this ILogger logger, string collectionVariable);

    [ZLoggerMessage(LogLevel.Debug, "Executing foreach iteration {iteration} (item variable: '{itemVariable}')")]
    public static partial void LogForeachIteration(this ILogger logger, int iteration, string itemVariable);

    [ZLoggerMessage(LogLevel.Warning, "Foreach iteration {iteration} failed — continuing execution (continue_on_error = true)")]
    public static partial void LogForeachIterationFailedContinuing(this ILogger logger, int iteration);

    [ZLoggerMessage(LogLevel.Warning, "Foreach iteration {iteration} failed — aborting loop")]
    public static partial void LogForeachIterationFailedAborting(this ILogger logger, int iteration);

    [ZLoggerMessage(LogLevel.Warning, "Foreach loop canceled after {completedIterations} iteration(s)")]
    public static partial void LogForeachCanceled(this ILogger logger, int completedIterations);

    [ZLoggerMessage(LogLevel.Debug, "Foreach loop completed {completedIterations} iteration(s) with status '{status}'")]
    public static partial void LogForeachCompleted(this ILogger logger, int completedIterations, string status);
}
