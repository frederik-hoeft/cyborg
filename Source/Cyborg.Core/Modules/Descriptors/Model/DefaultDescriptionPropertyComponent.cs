using Cyborg.Core.Modules.Descriptors.Writers;
using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Descriptors.Model;

internal sealed record DefaultDescriptionPropertyComponent(
    string Name,
    IDescriptionValueComponent Value,
    ImmutableArray<string> Hints) : IDescriptionPropertyComponent
{
    public ValueTask AcceptAsync(
        IDescriptionComponentWriter writer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writer);
        return writer.WriteAsync(this, cancellationToken);
    }
}
