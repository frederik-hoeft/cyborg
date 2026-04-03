using Cyborg.Core.Common.Pipelines;
using Cyborg.Modules.Borg.Shared.Json;
using Cyborg.Modules.Borg.Shared.Json.Logging;
using Cyborg.Modules.Borg.Shared.Output.Deserializers;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Cyborg.Modules.Borg.Shared.Output;

public sealed class BorgOutputLineParser
(
    BorgOutputJsonSerializerContext serializerContext,
    IBorgOutputDeserializerRegistry deserializerRegistry,
    IEnumerable<IBorgOutputLineProcessor> lineProcessors
) : IBorgOutputLineParser
{
    private readonly ImmutableArray<IBorgOutputLineProcessor> _processorChain = lineProcessors.CreatePipeline();

    public bool TryReadLine(ReadOnlySpan<char> json, [NotNullWhen(true)] out BorgJsonLine? line)
    {
        if (json.Trim().IsEmpty)
        {
            line = null;
            return false;
        }

        try
        {
            BorgJsonLineHeader? header = JsonSerializer.Deserialize(json, serializerContext.BorgJsonLineHeader);
            if (header is null || string.IsNullOrWhiteSpace(header.Type) || !deserializerRegistry.TryGetDeserializer(header.Type, out IBorgOutputDeserializer? deserializer))
            {
                line = null;
                return false;
            }

            return deserializer.TryDeserialize(json, out line);
        }
        catch (JsonException)
        {
            line = null;
            return false;
        }
    }

    public bool TryReadLine<T>(ReadOnlySpan<char> json, [NotNullWhen(true)] out T? line) where T : BorgJsonLine
    {
        if (TryReadLine(json, out BorgJsonLine? baseLine) && baseLine is T typedLine)
        {
            line = typedLine;
            return true;
        }
        line = null;
        return false;
    }

    public async ValueTask<bool> TryProcessLineAsync(string json, CancellationToken cancellationToken)
    {
        if (!TryReadLine(json, out BorgJsonLine? line))
        {
            return false;
        }
        foreach (IBorgOutputLineProcessor processor in _processorChain)
        {
            bool processed = await processor.TryProcessLineAsync(line, cancellationToken);
            if (processed)
            {
                return true;
            }
        }
        return false;
    }
}
