using System.Text.Json.Serialization;

namespace Cyborg.Core.Configuration;

public sealed class WakeOnLanConfiguration
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    [JsonPropertyName("timeout")]
    public int Timeout { get; init; } = 60;

    [JsonPropertyName("retries")]
    public int Retries { get; init; } = 3;
}
