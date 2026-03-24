using System.Collections.Immutable;
using System.Text.Json;

namespace Cyborg.Modules.Borg.Create.Metrics;

public sealed record BorgCreateArchive(
    ImmutableArray<string> CommandLine,
    double Duration,
    DateTime End,
    string Id,
    BorgArchiveLimits Limits,
    string Name,
    DateTime Start,
    BorgArchiveStats Stats,
    ImmutableArray<JsonElement>? ChunkerParams = null
);
