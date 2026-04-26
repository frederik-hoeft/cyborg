namespace Cyborg.Modules.Borg.Prune.Metrics.Model;

public sealed record BorgPruneLineModel
(
    BorgPruneAction Action,
    string ArchiveName,
    DateTime ArchiveTimestamp,
    string ArchiveId
);
