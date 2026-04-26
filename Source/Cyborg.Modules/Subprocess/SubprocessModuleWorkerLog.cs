using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Modules.Subprocess;

internal static partial class SubprocessModuleWorkerLog
{
    [ZLoggerMessage(LogLevel.Warning, "Subprocess '{executable}' exited with non-zero code {exitCode} — check exit code is enabled")]
    public static partial void LogSubprocessFailed(this ILogger logger, string executable, int exitCode);
}
