using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cyborg.Modules.Sequence;

internal static partial class SequenceModuleWorkerLog
{
    [ZLoggerMessage(LogLevel.Debug, "Executing sequence step {step} of {total}")]
    public static partial void LogSequenceStep(this ILogger logger, int step, int total);

    [ZLoggerMessage(LogLevel.Warning, "Sequence step {step} of {total} failed or was canceled with status '{status}' — aborting sequence")]
    public static partial void LogSequenceStepAborted(this ILogger logger, int step, int total, string status);

    [ZLoggerMessage(LogLevel.Warning, "Sequence canceled before executing step {nextStep} of {total}")]
    public static partial void LogSequenceCanceled(this ILogger logger, int nextStep, int total);

    [ZLoggerMessage(LogLevel.Debug, "Sequence completed with status '{status}'")]
    public static partial void LogSequenceCompleted(this ILogger logger, string status);
}
