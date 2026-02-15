using System.Text.Json.Serialization;

namespace Cyborg.Core.Configuration;

public sealed class BackupHostConfiguration
{
    [JsonPropertyName("hostname")]
    public required string Hostname { get; init; }

    [JsonPropertyName("port")]
    public required int Port { get; init; }

    [JsonPropertyName("macAddress")]
    public string? MacAddress { get; init; }

    [JsonPropertyName("repository")]
    public required string Repository { get; init; }
}
