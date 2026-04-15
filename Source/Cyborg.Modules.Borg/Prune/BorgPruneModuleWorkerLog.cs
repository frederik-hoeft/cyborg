using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Modules.Borg.Prune;

internal static partial class BorgPruneModuleWorkerLog
{
    [ZLoggerMessage(LogLevel.Warning, "Borg prune did not produce any output for repository '{remoteRepository}'.")]
    public static partial void LogBorgPruneNoOutput(this ILogger logger, string remoteRepository);
}
