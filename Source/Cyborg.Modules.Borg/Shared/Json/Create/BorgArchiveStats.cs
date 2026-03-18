namespace Cyborg.Modules.Borg.Shared.Json.Create;

public sealed record BorgArchiveStats(
    long CompressedSize,
    long DeduplicatedSize,
    long Nfiles,
    long OriginalSize
);
