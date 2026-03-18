namespace Cyborg.Modules.Borg.Shared.Json.Create;

public sealed record BorgCacheStats(
    long TotalChunks,
    long TotalCsize,
    long TotalSize,
    long TotalUniqueChunks,
    long UniqueCsize,
    long UniqueSize
);
