using Cyborg.Core.Modules.Descriptors.Writers;
using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Descriptors.Model;

public sealed record DefaultDescriptionObjectComponent(
    ImmutableArray<IDescriptionPropertyComponent> Properties,
    ImmutableArray<string> Hints) : IDescriptionObjectComponent
{
    public ValueTask AcceptAsync(
        IDescriptionComponentWriter writer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writer);
        return writer.WriteAsync(this, cancellationToken);
    }
}
