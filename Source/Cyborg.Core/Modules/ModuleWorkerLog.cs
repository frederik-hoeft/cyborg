using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Core.Modules;

internal static partial class ModuleWorkerLog
{
    [ZLoggerMessage(LogLevel.Debug, "Validating module '{moduleId}'")]
    public static partial void LogModuleValidationStarted(this ILogger logger, string moduleId);

    [ZLoggerMessage(LogLevel.Debug, "Validation completed for module '{moduleId}'")]
    public static partial void LogModuleValidationCompleted(this ILogger logger, string moduleId);

    [ZLoggerMessage(LogLevel.Warning, "Validation failed for module '{moduleId}': {reason}")]
    public static partial void LogModuleValidationFailed(this ILogger logger, string moduleId, string reason);
}
