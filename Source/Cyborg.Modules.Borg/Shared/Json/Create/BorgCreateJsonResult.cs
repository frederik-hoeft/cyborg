namespace Cyborg.Modules.Borg.Shared.Json.Create;

public sealed record BorgCreateJsonResult(
    BorgCreateArchive Archive,
    BorgRepositoryInfo Repository,
    BorgEncryptionInfo? Encryption,
    BorgCacheInfo? Cache
);
