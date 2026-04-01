using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Modules.If;

internal static partial class IfModuleWorkerLog
{
    [ZLoggerMessage(LogLevel.Debug, "Evaluating if condition '{conditionModuleId}'")]
    public static partial void LogIfConditionEvaluating(this ILogger logger, string conditionModuleId);

    [ZLoggerMessage(LogLevel.Error, "If condition '{conditionModuleId}' did not succeed (status: '{status}') — skipping branches")]
    public static partial void LogIfConditionFailed(this ILogger logger, string conditionModuleId, string status);

    [ZLoggerMessage(LogLevel.Error, "If condition '{conditionModuleId}' result could not be read — treating as failure")]
    public static partial void LogIfConditionResultUnreadable(this ILogger logger, string conditionModuleId);

    [ZLoggerMessage(LogLevel.Debug, "If condition evaluated to {result} — executing '{branch}' branch")]
    public static partial void LogIfBranchTaken(this ILogger logger, bool result, string branch);

    [ZLoggerMessage(LogLevel.Debug, "If condition evaluated to {result} but no matching branch is configured — skipping")]
    public static partial void LogIfNoBranch(this ILogger logger, bool result);
}
