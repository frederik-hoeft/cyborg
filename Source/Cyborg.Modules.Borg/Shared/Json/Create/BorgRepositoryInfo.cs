namespace Cyborg.Modules.Borg.Shared.Json.Create;

public sealed record BorgRepositoryInfo(
    string Id,
    DateTime LastModified,
    string Location
);
