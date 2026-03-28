using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Cli.Logging;

internal static partial class CliLog
{
    [ZLoggerMessage(LogLevel.Information, "Starting execution of template: {template}")]
    public static partial void LogRunStarted(this ILogger logger, string template);

    [ZLoggerMessage(LogLevel.Information, "Template {template} executed successfully")]
    public static partial void LogRunCompleted(this ILogger logger, string template);

    [ZLoggerMessage(LogLevel.Warning, "Template {template} execution completed with status: {status}")]
    public static partial void LogRunCompletedWithStatus(this ILogger logger, string template, string status);
}
