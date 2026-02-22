using System.Text.Json.Serialization;

namespace Cyborg.Core.Logging;

public sealed record JsonLogEntry(
    [property: JsonPropertyName("timestamp")] string Timestamp,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("exception")] string? Exception
);
