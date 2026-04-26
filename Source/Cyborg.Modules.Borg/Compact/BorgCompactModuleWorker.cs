using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Services.Dispatch;
using Cyborg.Core.Services.Metrics;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Cyborg.Modules.Borg.Compact;

public sealed partial class BorgCompactModuleWorker
(
    IWorkerContext<BorgCompactModule> context,
    IChildProcessDispatcher processDispatcher,
    IPosixShellCommandBuilder commandBuilder,
    IMetricsCollector metricsCollector
) : BorgModuleWorker<BorgCompactModule>(context, commandBuilder)
{
    private const string BORG_COMPACT_LAST_EXIT_CODE = "borg_compact_last_exit_code";
    private const string BORG_COMPACT_LAST_DURATION_SECONDS = "borg_compact_last_duration_seconds";
    private const string BORG_COMPACT_LAST_RUN_SUCCESS = "borg_compact_last_run_success";
    private const string BORG_COMPACT_LAST_RECLAIMED_BYTES = "borg_compact_last_reclaimed_bytes";

    private readonly Stopwatch _stopwatch = new();

    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        if (IsDryRun(runtime))
        {
            // borg compact doesn't have a dry-run mode, so we just skip execution and return success.
            return runtime.Exit(Success());
        }

        List<string> arguments =
        [
            "compact",
            "--verbose",
            "--threshold", Module.Threshold.ToString(),
        ];
        arguments.Add(Module.RemoteRepository.GetRepositoryUri());

        ProcessStartInfo startInfo = new(Module.Executable, arguments);
        AddDefaults(startInfo);

        _stopwatch.Restart();
        ChildProcessResult executionResult = await processDispatcher.ExecuteAsync(startInfo, cancellationToken);
        _stopwatch.Stop();

        CollectMetrics(runtime, executionResult);

        if (executionResult.ExitCode != 0)
        {
            return runtime.Exit(Failed());
        }
        return runtime.Exit(Success());
    }

    protected override IMetricsLabelCollection AddDefaultLabels(IModuleRuntime runtime, IMetricsLabelCollection labels)
    {
        IMetricsLabelCollection labelsWithDefaults = base.AddDefaultLabels(runtime, labels);
        labelsWithDefaults.AddLabel("threshold", Module.Threshold.ToString(CultureInfo.InvariantCulture));
        return labelsWithDefaults;
    }

    private void CollectMetrics(IModuleRuntime runtime, ChildProcessResult executionResult)
    {
        IMetricsLabelCollection defaultLabels = AddDefaultLabels(runtime, metricsCollector.CreateLabels());
        int lastRunSuccess = executionResult.ExitCode == 0 ? 1 : 0;

        metricsCollector.AddGauge(BORG_COMPACT_LAST_EXIT_CODE, "Exit code of the borg compact command", samples => samples
            .Add(executionResult.ExitCode, defaultLabels));
        metricsCollector.AddGauge(BORG_COMPACT_LAST_DURATION_SECONDS, "Duration of the borg compact command in seconds", samples => samples
            .Add(_stopwatch.Elapsed.TotalSeconds, defaultLabels));
        metricsCollector.AddGauge(BORG_COMPACT_LAST_RUN_SUCCESS, "Whether the most recent borg compact command succeeded (1 for success, 0 for failure)", samples => samples
            .Add(lastRunSuccess, defaultLabels));

        if (executionResult.ExitCode != 0 || !TryReadReclaimedBytes(executionResult, out long reclaimedBytes))
        {
            return;
        }

        metricsCollector.AddGauge(BORG_COMPACT_LAST_RECLAIMED_BYTES, "Estimated repository bytes reclaimed by the most recent borg compact command", samples => samples
            .Add(reclaimedBytes, defaultLabels));
    }

    private static bool TryReadReclaimedBytes(ChildProcessResult executionResult, out long reclaimedBytes)
    {
        if (TryReadReclaimedBytes(executionResult.StandardError, out reclaimedBytes))
        {
            return true;
        }
        if (TryReadReclaimedBytes(executionResult.StandardOutput, out reclaimedBytes))
        {
            return true;
        }

        reclaimedBytes = 0;
        return false;
    }

    private static bool TryReadReclaimedBytes(string? output, out long reclaimedBytes)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            reclaimedBytes = 0;
            return false;
        }

        foreach (ReadOnlySpan<char> line in output.EnumerateLines())
        {
            if (TryParseReclaimedBytes(line, out reclaimedBytes))
            {
                return true;
            }
        }

        reclaimedBytes = 0;
        return false;
    }

    private static bool TryParseReclaimedBytes(ReadOnlySpan<char> line, out long reclaimedBytes)
    {
        Match match = ReclaimedBytesRegex.Match(line.ToString());
        if (!match.Success)
        {
            reclaimedBytes = 0;
            return false;
        }

        string valueText = match.Groups["value"].Value;
        string unitText = match.Groups["unit"].Value;

        return TryParseDecimalBytes(valueText, unitText, out reclaimedBytes);
    }

    private static bool TryParseDecimalBytes(string valueText, string unitText, out long bytes)
    {
        if (!decimal.TryParse(valueText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value))
        {
            bytes = 0;
            return false;
        }

        decimal multiplier = unitText switch
        {
            "B" => 1m,
            "kB" => 1_000m,
            "MB" => 1_000_000m,
            "GB" => 1_000_000_000m,
            "TB" => 1_000_000_000_000m,
            "PB" => 1_000_000_000_000_000m,
            "EB" => 1_000_000_000_000_000_000m,
            _ => 0m,
        };
        if (multiplier == 0m)
        {
            bytes = 0;
            return false;
        }

        bytes = decimal.ToInt64(decimal.Round(value * multiplier, 0, MidpointRounding.AwayFromZero));
        return true;
    }

    [GeneratedRegex(@"^(?:Remote:\s+)?compaction freed about (?<value>\d+(?:\.\d+)?) (?<unit>[kMGTPE]?B) repository space\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReclaimedBytesRegex { get; }
}
