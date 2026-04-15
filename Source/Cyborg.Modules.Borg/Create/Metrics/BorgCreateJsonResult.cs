namespace Cyborg.Modules.Borg.Create.Metrics;

public sealed record BorgCreateJsonResult(
    BorgCreateArchive Archive,
    BorgRepositoryInfo Repository,
    BorgEncryptionInfo? Encryption,
    BorgCacheInfo? Cache
);
