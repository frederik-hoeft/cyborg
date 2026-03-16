using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Services.Dispatch;
using Cyborg.Core.Services.Metrics;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Borg.Prune;

public sealed class BorgPruneModuleWorker
(
    IWorkerContext<BorgPruneModule> context,
    IPosixShellCommandBuilder shellCommandBuilder,
    IChildProcessDispatcher processDispatcher,
    IMetricsCollector metricsCollector
) : BorgModuleWorker<BorgPruneModule>(context, shellCommandBuilder)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        List<string> arguments =
        [
            "prune",
            "--list", "--log-json",
            "--checkpoint-interval", Module.CheckpointIntervalSeconds.ToString(),
        ];
        if (Module.SaveSpace)
        {
            arguments.Add("--save-space");
        }
        if (!string.IsNullOrWhiteSpace(Module.GlobArchives))
        {
            arguments.Add("--glob-archives");
            arguments.Add(Module.GlobArchives);
        }
        ReadOnlySpan<(int keep, string option)> keeps =
        [
            (Module.Keep.Last, "--keep-last"),
            (Module.Keep.Minutely, "--keep-minutely"),
            (Module.Keep.Hourly, "--keep-hourly"),
            (Module.Keep.Daily, "--keep-daily"),
            (Module.Keep.Weekly, "--keep-weekly"),
            (Module.Keep.Monthly, "--keep-monthly"),
            (Module.Keep.Yearly, "--keep-yearly"),
            (Module.Keep.Weekly13, "--keep-13weekly"),
            (Module.Keep.Monthly3, "--keep-3monthly"),
        ];
        foreach ((int keep, string option) in keeps)
        {
            if (keep > 0)
            {
                arguments.Add(option);
                arguments.Add(keep.ToString());
            }
        }
        arguments.Add(Module.RemoteRepository.GetRepositoryUri());
        ProcessStartInfo startInfo = new(Module.Executable, arguments);
        AddDefaults(startInfo);
        ChildProcessResult executionResult = await processDispatcher.ExecuteAsync(startInfo, cancellationToken);
        if (executionResult.ExitCode != 0)
        {
            return runtime.Exit(Failed());
        }
        // TODO: output parsing and metric extraction
        return runtime.Exit(Success());
    }
}