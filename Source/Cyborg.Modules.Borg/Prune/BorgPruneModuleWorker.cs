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
using System.Globalization;

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
    private const string BORG_PRUNE_LAST_EXIT_CODE = "borg_prune_last_exit_code";
    private const string BORG_PRUNE_LAST_DURATION_SECONDS = "borg_prune_last_duration_seconds";
    private const string BORG_PRUNE_LAST_RUN_SUCCESS = "borg_prune_last_run_success";
    private const string BORG_PRUNE_LAST_DELETED_ARCHIVES = "borg_prune_last_deleted_archives";
    private const string BORG_PRUNE_LAST_KEPT_ARCHIVES_BY_RULE = "borg_prune_last_kept_archives_by_rule";
    private const string BORG_BACKUP_LATEST_TIMESTAMP_SECONDS = "borg_backup_latest_timestamp_seconds";
    private const string BORG_BACKUP_OLDEST_TIMESTAMP_SECONDS = "borg_backup_oldest_timestamp_seconds";
    private const string BORG_RETAINED_BACKUP_TIMESTAMP_SECONDS = "borg_retained_backup_timestamp_seconds";

    private readonly Stopwatch _stopwatch = new();

    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        List<string> arguments =
        [
            "prune",
            "--list", "--log-json",
            "--checkpoint-interval", Module.CheckpointIntervalSeconds.ToString(),
        ];
        if (IsDryRun(runtime))
        {
            arguments.Add("--dry-run");
        }
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
        _stopwatch.Restart();
        ChildProcessResult executionResult = await processDispatcher.ExecuteAsync(startInfo, cancellationToken);
        _stopwatch.Stop();

        CollectMetrics(executionResult);

        if (executionResult.ExitCode != 0)
        {
            return runtime.Exit(Failed());
        }
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

    private void CollectMetrics(ChildProcessResult executionResult)
    {
        IMetricsLabelCollection defaultLabels = AddDefaultLabels(metricsCollector.CreateLabels());
        int lastRunSuccess = executionResult.ExitCode == 0 ? 1 : 0;
        int deletedArchives = 0;
        DateTime? latestRetainedArchive = null;
        DateTime? oldestRetainedArchive = null;
        Dictionary<string, List<BorgPruneLineModel>> retainedArchivesByRule = new(StringComparer.Ordinal);

        metricsCollector.AddGauge(BORG_PRUNE_LAST_EXIT_CODE, "Exit code of the borg prune command", samples => samples
            .Add(executionResult.ExitCode, defaultLabels));
        metricsCollector.AddGauge(BORG_PRUNE_LAST_DURATION_SECONDS, "Duration of the borg prune command in seconds", samples => samples
            .Add(_stopwatch.Elapsed.TotalSeconds, defaultLabels));
        metricsCollector.AddGauge(BORG_PRUNE_LAST_RUN_SUCCESS, "Whether the most recent borg prune command succeeded (1 for success, 0 for failure)", samples => samples
            .Add(lastRunSuccess, defaultLabels));

        if (executionResult.ExitCode != 0)
        {
            return;
        }

        if (executionResult.StandardOutput is { Length: > 0 } output)
        {
            foreach (ReadOnlySpan<char> jsonLine in output.EnumerateLines())
            {
                if (!outputLineParser.TryReadLine(jsonLine, out BorgLogMessageJsonLine? line)
                    || line is not { LevelName: BorgLogMessageJsonLine.INFO, Name: "borg.output.list", Message: { Length: > 0 } message }
                    || !BorgPruneLineGrammar.TryParse(message, out BorgPruneLineModel? model))
                {
                    Logger.LogBorgPruneLineGrammarFailed(jsonLine.ToString());
                    continue;
                }

                if (model.Action is BorgPruneKeepAction keepAction)
                {
                    if (!retainedArchivesByRule.TryGetValue(keepAction.RuleName, out List<BorgPruneLineModel>? retainedArchives))
                    {
                        retainedArchives = [];
                        retainedArchivesByRule.Add(keepAction.RuleName, retainedArchives);
                    }
                    retainedArchives.Add(model);

                    if (latestRetainedArchive is null || model.ArchiveTimestamp > latestRetainedArchive.Value)
                    {
                        latestRetainedArchive = model.ArchiveTimestamp;
                    }
                    if (oldestRetainedArchive is null || model.ArchiveTimestamp < oldestRetainedArchive.Value)
                    {
                        oldestRetainedArchive = model.ArchiveTimestamp;
                    }
                    continue;
                }
                if (model.Action is BorgPrunePruneAction)
                {
                    ++deletedArchives;
                }
            }
        }

        metricsCollector.AddGauge(BORG_PRUNE_LAST_DELETED_ARCHIVES, "Number of archives deleted by the most recent borg prune command", samples => samples
            .Add(deletedArchives, defaultLabels));

        if (latestRetainedArchive is { } latestArchive)
        {
            metricsCollector.AddGauge(BORG_BACKUP_LATEST_TIMESTAMP_SECONDS, "Unix timestamp in seconds of the newest retained backup after the most recent borg prune command", samples => samples
                .Add(new DateTimeOffset(latestArchive).ToUnixTimeSeconds(), defaultLabels));
        }
        if (oldestRetainedArchive is { } oldestArchive)
        {
            metricsCollector.AddGauge(BORG_BACKUP_OLDEST_TIMESTAMP_SECONDS, "Unix timestamp in seconds of the oldest retained backup after the most recent borg prune command", samples => samples
                .Add(new DateTimeOffset(oldestArchive).ToUnixTimeSeconds(), defaultLabels));
        }
        if (retainedArchivesByRule.Count == 0)
        {
            return;
        }

        metricsCollector.AddGauge(BORG_PRUNE_LAST_KEPT_ARCHIVES_BY_RULE, "Number of archives retained by the most recent borg prune command", samples =>
        {
            foreach ((string ruleName, List<BorgPruneLineModel> retainedArchives) in retainedArchivesByRule)
            {
                samples.Add(retainedArchives.Count, labels => labels
                    .Add(defaultLabels)
                    .AddLabel("rule", ruleName));
            }
        });

        metricsCollector.AddGauge(BORG_RETAINED_BACKUP_TIMESTAMP_SECONDS, "Unix timestamp in seconds of a retained backup after the most recent borg prune command", samples =>
        {
            foreach ((string ruleName, List<BorgPruneLineModel> retainedArchives) in retainedArchivesByRule)
            {
                retainedArchives.Sort(static (left, right) =>
                {
                    int byTimestamp = right.ArchiveTimestamp.CompareTo(left.ArchiveTimestamp);
                    if (byTimestamp != 0)
                    {
                        return byTimestamp;
                    }

                    int byName = StringComparer.Ordinal.Compare(left.ArchiveName, right.ArchiveName);
                    if (byName != 0)
                    {
                        return byName;
                    }

                    return StringComparer.Ordinal.Compare(left.ArchiveId, right.ArchiveId);
                });

                for (int i = 0; i < retainedArchives.Count; i++)
                {
                    BorgPruneLineModel retainedArchive = retainedArchives[i];
                    string slot = (i + 1).ToString("D2", CultureInfo.InvariantCulture);
                    long timestampSeconds = new DateTimeOffset(retainedArchive.ArchiveTimestamp).ToUnixTimeSeconds();
                    samples.Add(timestampSeconds, labels => labels
                        .Add(defaultLabels)
                        .AddLabel("rule", ruleName)
                        .AddLabel("slot", slot));
                }
            }
        });
    }
}