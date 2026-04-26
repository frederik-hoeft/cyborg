using Cyborg.Core.Services.Dispatch;

namespace Cyborg.Modules.Borg.Prune;

internal static class BorgPruneOutputReader
{
    public static IEnumerable<string> GetOutputs(ChildProcessResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (!string.IsNullOrWhiteSpace(executionResult.StandardError))
        {
            yield return executionResult.StandardError;
        }
        if (!string.IsNullOrWhiteSpace(executionResult.StandardOutput))
        {
            yield return executionResult.StandardOutput;
        }
    }
}
