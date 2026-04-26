using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Modules.Switch;

internal static partial class SwitchModuleWorkerLog
{
    [ZLoggerMessage(LogLevel.Error, "Failed to resolve switch variable '{variableName}' from the current environment")]
    public static partial void LogSwitchVariableNotFound(this ILogger logger, string variableName);

    [ZLoggerMessage(LogLevel.Error, "No case matching '{caseName}' found in switch module")]
    public static partial void LogSwitchCaseNotFound(this ILogger logger, string caseName);

    [ZLoggerMessage(LogLevel.Debug, "Switch selected case '{caseName}' (template: '{templatePath}')")]
    public static partial void LogSwitchCaseSelected(this ILogger logger, string caseName, string templatePath);
}
