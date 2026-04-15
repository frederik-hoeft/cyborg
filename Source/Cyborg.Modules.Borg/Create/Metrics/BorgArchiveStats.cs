namespace Cyborg.Modules.Borg.Create.Metrics;

public sealed record BorgArchiveStats(
    long CompressedSize,
    long DeduplicatedSize,
    long Nfiles,
    long OriginalSize
);
