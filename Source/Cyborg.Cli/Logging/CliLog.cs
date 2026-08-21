using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Cli.Logging;

internal static partial class CliLog
{
    [ZLoggerMessage(LogLevel.Information, "Cyborg started with effective arguments: {arguments}")]
    public static partial void LogStartup(this ILogger logger, string arguments);

    [ZLoggerMessage(LogLevel.Information, "Starting execution of backup target: {target}")]
    public static partial void LogRunStarted(this ILogger logger, string target);

    [ZLoggerMessage(LogLevel.Information, "Backup target {target} executed successfully")]
    public static partial void LogRunCompleted(this ILogger logger, string target);

    [ZLoggerMessage(LogLevel.Warning, "Backup target {target} execution completed with status: {status}")]
    public static partial void LogRunCompletedWithStatus(this ILogger logger, string target, string status);

    [ZLoggerMessage(LogLevel.Error, "Invalid environment variable definition '{definition}'. Reason: {reason}")]
    public static partial void LogInvalidEnvironmentVariable(this ILogger logger, string definition, string reason);

    [ZLoggerMessage(LogLevel.Error, "Invalid --config definition {message}")]
    public static partial void LogInvalidConfigurationOverride(this ILogger logger, string message);

    [ZLoggerMessage(LogLevel.Error, "Cannot use --break-at with no debug frontend configured. Configure a debug frontend at '{configKey}' or remove the --break-at option.")]
    public static partial void LogBreakAtWithoutDebugFrontend(this ILogger logger, string configKey);

    [ZLoggerMessage(LogLevel.Error, "Invalid --break-at expression '{expression}': {message}")]
    public static partial void LogInvalidBreakAtExpression(this ILogger logger, string expression, string message);
}
