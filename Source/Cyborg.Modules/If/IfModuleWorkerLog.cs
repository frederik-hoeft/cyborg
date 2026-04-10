using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Modules.If;

internal static partial class IfModuleWorkerLog
{
    [ZLoggerMessage(LogLevel.Debug, "If condition evaluated to {result} — executing '{branch}' branch")]
    public static partial void LogIfBranchTaken(this ILogger logger, bool result, string branch);

    [ZLoggerMessage(LogLevel.Debug, "If condition evaluated to {result} but no matching branch is configured — skipping")]
    public static partial void LogIfNoBranch(this ILogger logger, bool result);
}
