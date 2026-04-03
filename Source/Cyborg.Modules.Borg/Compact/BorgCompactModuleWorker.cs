using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Services.Dispatch;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Borg.Compact;

public sealed class BorgCompactModuleWorker
(
    IWorkerContext<BorgCompactModule> context,
    IChildProcessDispatcher processDispatcher,
    IPosixShellCommandBuilder commandBuilder
) : BorgModuleWorker<BorgCompactModule>(context, commandBuilder)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        List<string> arguments =
        [
            "compact",
            "--threshold", Module.Threshold.ToString(),
        ];
        if (IsDryRun(runtime))
        {
            // borg compact doesn't have a dry-run mode, so we just skip execution and return success.
            return runtime.Exit(Success());
        }
        arguments.Add(Module.RemoteRepository.GetRepositoryUri());
        ProcessStartInfo startInfo = new(Module.Executable, arguments);
        AddDefaults(startInfo);
        ChildProcessResult executionResult = await processDispatcher.ExecuteAsync(startInfo, cancellationToken);
        if (executionResult.ExitCode != 0)
        {
            return runtime.Exit(Failed());
        }
        return runtime.Exit(Success());
    }
}