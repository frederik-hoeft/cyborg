using System.Text.Json;

namespace Cyborg.Modules.Borg.Shared.Json.Logging;

public sealed record BorgUnknownJsonLine(
    string Type,
    JsonElement Payload) : BorgJsonLine(Type);
