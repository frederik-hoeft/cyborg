using System.Text.Json.Serialization;

namespace Cyborg.Core.Configuration;

public sealed class BackupJobConfiguration
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("hosts")]
    public required List<BackupHostConfiguration> Hosts { get; init; }

    [JsonPropertyName("wakeOnLan")]
    public WakeOnLanConfiguration? WakeOnLan { get; init; }

    [JsonPropertyName("docker")]
    public DockerConfiguration? Docker { get; init; }

    [JsonPropertyName("borg")]
    public required BorgConfiguration Borg { get; init; }

    [JsonPropertyName("passphrase")]
    public required string Passphrase { get; init; }
}
