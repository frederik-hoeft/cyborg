using Cyborg.Modules.Borg.Shared.Output.Deserializers;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Borg.Shared.Output;

public sealed class BorgOutputDeserializerRegistry(IEnumerable<IBorgOutputDeserializer> deserializers) : IBorgOutputDeserializerRegistry
{
    private readonly FrozenDictionary<string, IBorgOutputDeserializer> _deserializers = deserializers.ToFrozenDictionary(d => d.SupportedType, StringComparer.Ordinal);

    public bool TryGetDeserializer(string type, [NotNullWhen(true)] out IBorgOutputDeserializer? deserializer) => _deserializers.TryGetValue(type, out deserializer);
}