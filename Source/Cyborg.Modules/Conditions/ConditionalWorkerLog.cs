using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Modules.Conditions;

internal static partial class ConditionalWorkerLog
{
    [ZLoggerMessage(LogLevel.Debug, "Evaluating condition '{conditionModuleId}'")]
    public static partial void LogConditionEvaluating(this ILogger logger, string conditionModuleId);

    [ZLoggerMessage(LogLevel.Error, "Evaluation of condition '{conditionModuleId}' did not succeed (status: '{status}') — treating as failure")]
    public static partial void LogConditionFailed(this ILogger logger, string conditionModuleId, string status);

    [ZLoggerMessage(LogLevel.Error, "Result of condition '{conditionModuleId}' could not be read — treating as failure")]
    public static partial void LogConditionResultUnreadable(this ILogger logger, string conditionModuleId);

    [ZLoggerMessage(LogLevel.Debug, "Condition '{conditionModuleId}' evaluated to {result}")]
    public static partial void LogConditionEvaluated(this ILogger logger, string conditionModuleId, bool result);
}
