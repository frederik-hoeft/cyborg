namespace Cyborg.Modules.Borg.Create.Metrics;

public sealed record BorgCacheStats(
    long TotalChunks,
    long TotalCsize,
    long TotalSize,
    long TotalUniqueChunks,
    long UniqueCsize,
    long UniqueSize
);
