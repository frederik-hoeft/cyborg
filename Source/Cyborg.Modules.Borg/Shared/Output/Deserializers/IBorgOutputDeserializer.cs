using Cyborg.Modules.Borg.Shared.Json.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Borg.Shared.Output.Deserializers;

public interface IBorgOutputDeserializer
{
    string SupportedType { get; }

    bool TryDeserialize(ReadOnlySpan<char> json, [NotNullWhen(true)] out BorgJsonLine? line);
}
