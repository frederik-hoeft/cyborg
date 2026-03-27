using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Cli.Logging;

internal static partial class CliLog
{
    [ZLoggerMessage(LogLevel.Information, "Starting execution of template: {template}")]
    public static partial void LogRunStarted(this ILogger logger, string template);

    [ZLoggerMessage(LogLevel.Information, "Template {template} executed successfully")]
    public static partial void LogRunCompleted(this ILogger logger, string template);

    [ZLoggerMessage(LogLevel.Error, "Template {template} execution failed")]
    public static partial void LogRunFailed(this ILogger logger, string template, Exception exception);
}
