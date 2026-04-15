using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Services.Dispatch;
using Cyborg.Core.Services.Metrics;
using Cyborg.Modules.Borg.Create.Metrics;
using Cyborg.Modules.Borg.Shared.Json;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Cyborg.Modules.Borg.Create;

public sealed class BorgCreateModuleWorker
(
    IWorkerContext<BorgCreateModule> context,
    IChildProcessDispatcher processDispatcher,
    IPosixShellCommandBuilder shellCommandBuilder,
    IMetricsCollector metricsCollector
) : BorgModuleWorker<BorgCreateModule>(context, shellCommandBuilder)
{
    private const string BORG_CREATE_LAST_EXIT_CODE = "borg_create_last_exit_code";
    private const string BORG_CREATE_LAST_DURATION_SECONDS = "borg_create_last_duration_seconds";
    private const string BORG_CREATE_LAST_RUN_SUCCESS = "borg_create_last_run_success";

    private const string BORG_CREATE_LAST_ARCHIVE_DURATION_SECONDS = "borg_create_last_archive_duration_seconds";
    private const string BORG_CREATE_LAST_ARCHIVE_START_TIMESTAMP_SECONDS = "borg_create_last_archive_start_timestamp_seconds";
    private const string BORG_CREATE_LAST_ARCHIVE_END_TIMESTAMP_SECONDS = "borg_create_last_archive_end_timestamp_seconds";
    private const string BORG_BACKUP_LATEST_TIMESTAMP_SECONDS = "borg_backup_latest_timestamp_seconds";
    private const string BORG_CREATE_LAST_ARCHIVE_ORIGINAL_SIZE_BYTES = "borg_create_last_archive_original_size_bytes";
    private const string BORG_CREATE_LAST_ARCHIVE_COMPRESSED_SIZE_BYTES = "borg_create_last_archive_compressed_size_bytes";
    private const string BORG_CREATE_LAST_ARCHIVE_DEDUPLICATED_SIZE_BYTES = "borg_create_last_archive_deduplicated_size_bytes";
    private const string BORG_CREATE_LAST_ARCHIVE_FILE_COUNT = "borg_create_last_archive_file_count";
    private const string BORG_CREATE_LAST_ARCHIVE_MAX_SIZE_BYTES = "borg_create_last_archive_max_size_bytes";

    private const string BORG_REPOSITORY_LAST_MODIFIED_TIMESTAMP_SECONDS = "borg_repository_last_modified_timestamp_seconds";
    private const string BORG_REPOSITORY_CACHE_TOTAL_CHUNKS = "borg_repository_cache_total_chunks";
    private const string BORG_REPOSITORY_CACHE_TOTAL_SIZE_BYTES = "borg_repository_cache_total_size_bytes";
    private const string BORG_REPOSITORY_CACHE_TOTAL_COMPRESSED_SIZE_BYTES = "borg_repository_cache_total_compressed_size_bytes";
    private const string BORG_REPOSITORY_CACHE_TOTAL_UNIQUE_CHUNKS = "borg_repository_cache_total_unique_chunks";
    private const string BORG_REPOSITORY_CACHE_UNIQUE_SIZE_BYTES = "borg_repository_cache_unique_size_bytes";
    private const string BORG_REPOSITORY_CACHE_UNIQUE_COMPRESSED_SIZE_BYTES = "borg_repository_cache_unique_compressed_size_bytes";

    private readonly Stopwatch _stopwatch = new();

    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        List<string> arguments =
        [
            "create",
            "--stats", "--json",
            "--compression", Module.Compression
        ];
        if (IsDryRun(runtime))
        {
            arguments.Add("--dry-run");
        }
        if (Module.Exclude.Caches)
        {
            arguments.Add("--exclude-caches");
        }
        foreach (string path in Module.Exclude.Paths)
        {
            arguments.Add("--exclude");
            arguments.Add(path);
        }
        arguments.Add($"{Module.RemoteRepository.GetRepositoryUri()}::{Module.ArchiveName}");
        arguments.Add(Module.SourcePath);

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
        labelsWithDefaults.AddLabel("compression", Module.Compression);
        labelsWithDefaults.AddLabel("exclude_caches", Module.Exclude.Caches ? "true" : "false");
        return labelsWithDefaults;
    }

    private void CollectMetrics(IModuleRuntime runtime, ChildProcessResult executionResult)
    {
        IMetricsLabelCollection defaultLabels = AddDefaultLabels(runtime, metricsCollector.CreateLabels());
        int lastRunSuccess = executionResult.ExitCode == 0 ? 1 : 0;

        metricsCollector.AddGauge(BORG_CREATE_LAST_EXIT_CODE, "Exit code of the borg create command", samples => samples
            .Add(executionResult.ExitCode, defaultLabels));

        metricsCollector.AddGauge(BORG_CREATE_LAST_DURATION_SECONDS, "Wall-clock duration of the borg create command in seconds", samples => samples
            .Add(_stopwatch.Elapsed.TotalSeconds, defaultLabels));

        metricsCollector.AddGauge(BORG_CREATE_LAST_RUN_SUCCESS, "Whether the most recent borg create command succeeded (1 for success, 0 for failure)", samples => samples
            .Add(lastRunSuccess, defaultLabels));

        if (executionResult.ExitCode != 0 || !TryReadCreateResult(executionResult, out BorgCreateJsonResult? createResult))
        {
            return;
        }

        BorgCreateArchive archive = createResult.Archive;
        BorgRepositoryInfo repository = createResult.Repository;

        metricsCollector.AddGauge(BORG_CREATE_LAST_ARCHIVE_DURATION_SECONDS, "Duration reported by borg for the created archive in seconds", samples => samples
            .Add(archive.Duration, defaultLabels));

        metricsCollector.AddGauge(BORG_CREATE_LAST_ARCHIVE_START_TIMESTAMP_SECONDS, "Unix timestamp in seconds when the created archive started", samples => samples
            .Add(new DateTimeOffset(archive.Start).ToUnixTimeSeconds(), defaultLabels));

        metricsCollector.AddGauge(BORG_CREATE_LAST_ARCHIVE_END_TIMESTAMP_SECONDS, "Unix timestamp in seconds when the created archive finished", samples => samples
            .Add(new DateTimeOffset(archive.End).ToUnixTimeSeconds(), defaultLabels));

        metricsCollector.AddGauge(BORG_BACKUP_LATEST_TIMESTAMP_SECONDS, "Unix timestamp in seconds of the newest known backup after the most recent borg create command", samples => samples
            .Add(new DateTimeOffset(archive.End).ToUnixTimeSeconds(), defaultLabels));

        metricsCollector.AddGauge(BORG_CREATE_LAST_ARCHIVE_ORIGINAL_SIZE_BYTES, "Original size in bytes of the archive created by the most recent borg create command", samples => samples
            .Add(archive.Stats.OriginalSize, defaultLabels));

        metricsCollector.AddGauge(BORG_CREATE_LAST_ARCHIVE_COMPRESSED_SIZE_BYTES, "Compressed size in bytes of the archive created by the most recent borg create command", samples => samples
            .Add(archive.Stats.CompressedSize, defaultLabels));

        metricsCollector.AddGauge(BORG_CREATE_LAST_ARCHIVE_DEDUPLICATED_SIZE_BYTES, "Deduplicated size in bytes of the archive created by the most recent borg create command", samples => samples
            .Add(archive.Stats.DeduplicatedSize, defaultLabels));

        metricsCollector.AddGauge(BORG_CREATE_LAST_ARCHIVE_FILE_COUNT, "Number of files in the archive created by the most recent borg create command", samples => samples
            .Add(archive.Stats.Nfiles, defaultLabels));

        metricsCollector.AddGauge(BORG_CREATE_LAST_ARCHIVE_MAX_SIZE_BYTES, "Configured maximum archive size in bytes for the archive created by the most recent borg create command", samples => samples
            .Add(archive.Limits.MaxArchiveSize, defaultLabels));

        metricsCollector.AddGauge(BORG_REPOSITORY_LAST_MODIFIED_TIMESTAMP_SECONDS, "Unix timestamp in seconds when borg last modified the repository", samples => samples
            .Add(new DateTimeOffset(repository.LastModified).ToUnixTimeSeconds(), defaultLabels));

        if (createResult.Cache is { Stats: { } cacheStats })
        {
            metricsCollector.AddGauge(BORG_REPOSITORY_CACHE_TOTAL_CHUNKS, "Total number of chunks currently known to the borg cache", samples => samples
                .Add(cacheStats.TotalChunks, defaultLabels));

            metricsCollector.AddGauge(BORG_REPOSITORY_CACHE_TOTAL_SIZE_BYTES, "Total uncompressed size in bytes currently known to the borg cache", samples => samples
                .Add(cacheStats.TotalSize, defaultLabels));

            metricsCollector.AddGauge(BORG_REPOSITORY_CACHE_TOTAL_COMPRESSED_SIZE_BYTES, "Total compressed size in bytes currently known to the borg cache", samples => samples
                .Add(cacheStats.TotalCsize, defaultLabels));

            metricsCollector.AddGauge(BORG_REPOSITORY_CACHE_TOTAL_UNIQUE_CHUNKS, "Total number of unique chunks currently known to the borg cache", samples => samples
                .Add(cacheStats.TotalUniqueChunks, defaultLabels));

            metricsCollector.AddGauge(BORG_REPOSITORY_CACHE_UNIQUE_SIZE_BYTES, "Total uncompressed size in bytes of unique chunks currently known to the borg cache", samples => samples
                .Add(cacheStats.UniqueSize, defaultLabels));

            metricsCollector.AddGauge(BORG_REPOSITORY_CACHE_UNIQUE_COMPRESSED_SIZE_BYTES, "Total compressed size in bytes of unique chunks currently known to the borg cache", samples => samples
                .Add(cacheStats.UniqueCsize, defaultLabels));
        }
    }

    private static bool TryReadCreateResult(ChildProcessResult executionResult, [NotNullWhen(true)] out BorgCreateJsonResult? createResult)
    {
        if (string.IsNullOrWhiteSpace(executionResult.StandardOutput))
        {
            createResult = null;
            return false;
        }

        try
        {
            createResult = JsonSerializer.Deserialize(executionResult.StandardOutput, BorgOutputJsonSerializerContext.Default.BorgCreateJsonResult);

            return createResult is not null;
        }
        catch (JsonException)
        {
            createResult = null;
            return false;
        }
    }
}