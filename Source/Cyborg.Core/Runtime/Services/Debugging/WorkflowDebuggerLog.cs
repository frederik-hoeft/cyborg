using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Core.Modules.Debugging;

internal static partial class WorkflowDebuggerLog
{
    [ZLoggerMessage(LogLevel.Debug, "Breakpoint hit for module '{moduleIdentity}' (expression {expression})")]
    public static partial void LogBreakpointHit(this ILogger logger, string moduleIdentity, string expression);

    [ZLoggerMessage(LogLevel.Warning, "Debugger paused for module '{moduleIdentity}' because breakpoint expression {expression} could not be evaluated: {message}")]
    public static partial void LogBreakpointEvaluationFailed(this ILogger logger, string moduleIdentity, string expression, string message);


    [ZLoggerMessage(LogLevel.Debug, "Stepped to module '{moduleIdentity}'")]
    public static partial void LogStepPause(this ILogger logger, string moduleIdentity);
}
