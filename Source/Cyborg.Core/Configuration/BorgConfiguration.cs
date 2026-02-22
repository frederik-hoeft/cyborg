using System.Text.Json.Serialization;

namespace Cyborg.Core.Configuration;

public sealed class BorgConfiguration
{
    [JsonPropertyName("compression")]
    public string Compression { get; init; } = "zlib";

    [JsonPropertyName("archiveName")]
    public required string ArchiveName { get; init; }

    [JsonPropertyName("sourcePath")]
    public required string SourcePath { get; init; }

    [JsonPropertyName("excludePatterns")]
    public List<string> ExcludePatterns { get; init; } = new();

    [JsonPropertyName("keepDaily")]
    public int KeepDaily { get; init; } = 30;

    [JsonPropertyName("keepWeekly")]
    public int KeepWeekly { get; init; } = 12;

    [JsonPropertyName("keepMonthly")]
    public int KeepMonthly { get; init; } = 12;
}
