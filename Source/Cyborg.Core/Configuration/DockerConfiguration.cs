using System.Text.Json.Serialization;

namespace Cyborg.Core.Configuration;

public sealed class DockerConfiguration
{
    [JsonPropertyName("user")]
    public string? User { get; init; }

    [JsonPropertyName("composePath")]
    public required string ComposePath { get; init; }

    [JsonPropertyName("containerGroup")]
    public required string ContainerGroup { get; init; }
}
