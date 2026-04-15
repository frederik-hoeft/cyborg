using Cyborg.Modules.Borg.Shared.Json.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Borg.Shared.Output;

public interface IBorgOutputLineParser
{
    bool TryReadLine(ReadOnlySpan<char> json, [NotNullWhen(true)] out BorgJsonLine? line);

    bool TryReadLine<T>(ReadOnlySpan<char> json, [NotNullWhen(true)] out T? line) where T : BorgJsonLine;

    ValueTask<bool> TryProcessLineAsync(string json, CancellationToken cancellationToken);
}