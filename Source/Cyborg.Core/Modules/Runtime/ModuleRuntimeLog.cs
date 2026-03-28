using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Core.Modules.Runtime;

internal static partial class ModuleRuntimeLog
{
    [ZLoggerMessage(LogLevel.Debug, "Dispatching module '{moduleId}' in environment '{environment}'")]
    public static partial void LogModuleDispatched(this ILogger logger, string moduleId, string environment);

    [ZLoggerMessage(LogLevel.Debug, "Module '{moduleId}' completed with status '{status}' in environment '{environment}'")]
    public static partial void LogModuleCompleted(this ILogger logger, string moduleId, string status, string environment);
}
