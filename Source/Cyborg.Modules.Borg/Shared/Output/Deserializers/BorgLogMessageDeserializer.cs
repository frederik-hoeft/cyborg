using Cyborg.Modules.Borg.Shared.Json;
using Cyborg.Modules.Borg.Shared.Json.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Cyborg.Modules.Borg.Shared.Output.Deserializers;

public sealed class BorgLogMessageDeserializer(BorgOutputJsonSerializerContext serializerContext) : IBorgOutputDeserializer
{
    public string SupportedType => "log_message";

    public bool TryDeserialize(ReadOnlySpan<char> json, [NotNullWhen(true)] out BorgJsonLine? line)
    {
        BorgLogMessageJsonLine? logMessage = JsonSerializer.Deserialize(json, serializerContext.BorgLogMessageJsonLine);
        line = logMessage;
        return line is not null;
    }
}
