using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Core.Modules.Debugging;

internal static partial class WorkflowDebuggerLog
{
    [ZLoggerMessage(LogLevel.Debug, "Breakpoint hit for module '{moduleIdentity}' (expression {expression})")]
    public static partial void LogBreakpointHit(this ILogger logger, string moduleIdentity, string expression);
}
