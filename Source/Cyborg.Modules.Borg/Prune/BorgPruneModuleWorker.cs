using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Services.Dispatch;
using Cyborg.Core.Services.Metrics;
using Cyborg.Modules.Borg.Prune.Metrics;
using Cyborg.Modules.Borg.Prune.Metrics.Model;
using Cyborg.Modules.Borg.Shared.Json.Logging;
using Cyborg.Modules.Borg.Shared.Output;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Borg.Prune;

public sealed class BorgPruneModuleWorker
(
    IWorkerContext<BorgPruneModule> context,
    IPosixShellCommandBuilder shellCommandBuilder,
    IChildProcessDispatcher processDispatcher,
    IBorgOutputLineParser outputLineParser,
    IMetricsCollector metricsCollector
) : BorgModuleWorker<BorgPruneModule>(context, shellCommandBuilder)
{
    private const string BORG_PRUNE_EXIT_CODE_METRIC_NAME = "borg_prune_exit_code";
    private const string BORG_PRUNE_DURATION_METRIC_NAME = "borg_prune_duration_seconds";

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
        if (!string.IsNullOrEmpty(Module.GlobArchives))
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
        Stopwatch sw = Stopwatch.StartNew();
        ChildProcessResult executionResult = await processDispatcher.ExecuteAsync(startInfo, cancellationToken);
        sw.Stop();
        if (executionResult.ExitCode != 0)
        {
            return runtime.Exit(Failed());
        }
        CollectMetrics(executionResult, sw);
        return runtime.Exit(Success());
    }

    protected override IMetricsLabelCollection AddDefaultLabels(IMetricsLabelCollection labels)
    {
        IMetricsLabelCollection labelsWithDefaults = base.AddDefaultLabels(labels);
        labelsWithDefaults.AddLabel("save_space", Module.SaveSpace ? "true" : "false");
        if (!string.IsNullOrEmpty(Module.GlobArchives))
        {
            labelsWithDefaults.AddLabel("glob_archives", Module.GlobArchives);
        }
        return labelsWithDefaults;
    }

    private void CollectMetrics(ChildProcessResult executionResult, Stopwatch sw)
    {
        metricsCollector.AddGauge(BORG_PRUNE_EXIT_CODE_METRIC_NAME, "Exit code of the borg prune command", samples => samples
            .Add(executionResult.ExitCode, labels => AddDefaultLabels(labels)));
        metricsCollector.AddGauge(BORG_PRUNE_DURATION_METRIC_NAME, "Duration of the borg prune command in seconds", samples => samples
            .Add(sw.Elapsed.TotalSeconds, labels => AddDefaultLabels(labels)));
        if (executionResult.StandardOutput is not { Length: > 0 } output)
        {
            return;
        }

        foreach (ReadOnlySpan<char> jsonLine in output.EnumerateLines())
        {
            if (!outputLineParser.TryReadLine(jsonLine, out BorgLogMessageJsonLine? line)
                || line is not { LevelName: BorgLogMessageJsonLine.INFO, Name: "borg.output.list", Message: { Length: > 0 } message }
                || !BorgPruneLineGrammar.TryParse(message, out BorgPruneLineModel? model))
            {
                continue;
            }
            // TODO: metrics
        }
    }
}