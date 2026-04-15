namespace Cyborg.Modules.Borg.Create.Metrics;

public sealed record BorgRepositoryInfo(
    string Id,
    DateTime LastModified,
    string Location
);
