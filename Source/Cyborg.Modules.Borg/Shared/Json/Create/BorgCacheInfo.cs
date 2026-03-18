namespace Cyborg.Modules.Borg.Shared.Json.Create;

public sealed record BorgCacheInfo(
    string Path,
    BorgCacheStats Stats
);
