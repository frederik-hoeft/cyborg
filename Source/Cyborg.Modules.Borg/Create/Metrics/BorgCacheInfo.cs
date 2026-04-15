namespace Cyborg.Modules.Borg.Create.Metrics;

public sealed record BorgCacheInfo(
    string Path,
    BorgCacheStats Stats
);
