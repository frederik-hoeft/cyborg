using Cyborg.Modules.Borg.Shared.Output.Deserializers;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Borg.Shared.Output;

public interface IBorgOutputDeserializerRegistry
{
    bool TryGetDeserializer(string type, [NotNullWhen(true)] out IBorgOutputDeserializer? deserializer);
}
