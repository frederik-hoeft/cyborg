using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Modules.While;

internal static partial class WhileModuleWorkerLog
{
    [ZLoggerMessage(LogLevel.Debug, "Evaluating while condition '{conditionModuleId}'")]
    public static partial void LogWhileConditionEvaluating(this ILogger logger, string conditionModuleId);

    [ZLoggerMessage(LogLevel.Error, "While condition '{conditionModuleId}' did not succeed (status: '{status}') — aborting loop")]
    public static partial void LogWhileConditionFailed(this ILogger logger, string conditionModuleId, string status);

    [ZLoggerMessage(LogLevel.Error, "While condition '{conditionModuleId}' result could not be read — treating as failure")]
    public static partial void LogWhileConditionResultUnreadable(this ILogger logger, string conditionModuleId);

    [ZLoggerMessage(LogLevel.Debug, "While condition evaluated to {result} — loop condition no longer met, exiting")]
    public static partial void LogWhileLoopExiting(this ILogger logger, bool result);

    [ZLoggerMessage(LogLevel.Debug, "While condition evaluated to {result} — executing body")]
    public static partial void LogWhileBodyExecuting(this ILogger logger, bool result);
}
