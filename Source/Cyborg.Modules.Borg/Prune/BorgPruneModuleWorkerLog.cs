using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Modules.Borg.Prune;

internal static partial class BorgPruneModuleWorkerLog
{
    [ZLoggerMessage(LogLevel.Warning, "Failed to parse borg prune output line as JSON or to match expected grammar: '{line}'")]
    public static partial void LogBorgPruneLineGrammarFailed(this ILogger logger, string line);

    [ZLoggerMessage(LogLevel.Warning, "Borg prune did not produce any output")]
    public static partial void LogBorgPruneNoOutput(this ILogger logger);
}
