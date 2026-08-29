using Cyborg.Core.Runtime.Services.ModuleDescriptors.Writers;
using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Services.ModuleDescriptors.Model;

internal sealed record DefaultDescriptionCollectionComponent(ImmutableArray<IDescriptionValueComponent> Items, ImmutableArray<string> Hints) : IDescriptionCollectionComponent
{
    public ValueTask AcceptAsync(IDescriptionComponentWriter writer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writer);
        return writer.WriteAsync(this, cancellationToken);
    }
}
