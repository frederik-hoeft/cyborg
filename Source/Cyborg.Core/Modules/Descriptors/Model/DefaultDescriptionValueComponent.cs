using Cyborg.Core.Modules.Descriptors.Writers;
using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Descriptors.Model;

internal sealed record DefaultDescriptionValueComponent<T>(
    T Value,
    ImmutableArray<string> Hints) : IDescriptionValueComponent
{
    public ValueTask AcceptAsync(
        IDescriptionComponentWriter writer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writer);
        return writer.WriteAtomAsync(Value, Hints, cancellationToken);
    }
}
