using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Cli.Logging;

internal static partial class CliLog
{
    [ZLoggerMessage(LogLevel.Information, "Cyborg started")]
    public static partial void LogStartup(this ILogger logger);

    [ZLoggerMessage(LogLevel.Information, "Starting execution of backup target: {target}")]
    public static partial void LogRunStarted(this ILogger logger, string target);

    [ZLoggerMessage(LogLevel.Information, "Backup target {target} executed successfully")]
    public static partial void LogRunCompleted(this ILogger logger, string target);

    [ZLoggerMessage(LogLevel.Warning, "Backup target {target} execution completed with status: {status}")]
    public static partial void LogRunCompletedWithStatus(this ILogger logger, string target, string status);

    [ZLoggerMessage(LogLevel.Error, "Environment variable definition is invalid. Expected format: 'key[:type]=value', where key must be a valid variable identifier and the optional type must be a valid registered dynamic value provider type name.")]
    public static partial void LogInvalidEnvironmentVariable(this ILogger logger);

    [ZLoggerMessage(LogLevel.Error, "Environment variable definition has an invalid type specification '{typeName}'. Expected format: 'key:type=value', where type must be a valid registered dynamic value provider type name.")]
    public static partial void LogUnknownEnvironmentVariableType(this ILogger logger, string typeName);

    [ZLoggerMessage(LogLevel.Error, "Invalid --config definition: {message}")]
    public static partial void LogInvalidConfigurationOverride(this ILogger logger, string message);

    [ZLoggerMessage(LogLevel.Error, "Cannot use --break-at with no debug frontend configured. Configure a debug frontend at '{configKey}' or remove the --break-at option.")]
    public static partial void LogBreakAtWithoutDebugFrontend(this ILogger logger, string configKey);

    [ZLoggerMessage(LogLevel.Error, "Invalid --break-at expression '{expression}': {message}")]
    public static partial void LogInvalidBreakAtExpression(this ILogger logger, string expression, string message);
}
