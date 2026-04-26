using System.Text.Json.Serialization;

namespace Cyborg.Modules.Borg.Shared.Json.Logging;

public record BorgLogMessageJsonLine(
    string Type,
    double Time,
    [property: JsonPropertyName("levelname")] string LevelName,
    string Name,
    string Message,
    [property: JsonPropertyName("msgid")] string? MsgId = null) : BorgJsonLine(Type)
{
    public const string INFO = "INFO";
}
