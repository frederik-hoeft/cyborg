using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Modules.Template;

internal static partial class TemplateModuleWorkerLog
{
    [ZLoggerMessage(LogLevel.Debug, "Loading template from '{templatePath}'")]
    public static partial void LogTemplateLoading(this ILogger logger, string templatePath);

    [ZLoggerMessage(LogLevel.Debug, "Loaded template '{moduleId}' from '{templatePath}'")]
    public static partial void LogTemplateLoaded(this ILogger logger, string moduleId, string templatePath);

    [ZLoggerMessage(LogLevel.Debug, "Applying {argumentCount} argument(s) and {overrideCount} override(s) to template '{templatePath}'")]
    public static partial void LogTemplateArgumentsApplied(this ILogger logger, int argumentCount, int overrideCount, string templatePath);

    [ZLoggerMessage(LogLevel.Error, "Template '{templatePath}' has invalid arguments or overrides: {errors}")]
    public static partial void LogTemplateConfigurationError(this ILogger logger, string templatePath, string errors);
}
