using Cyborg.Core.Runtime.Services.ModuleDescriptors.Writers;
using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Services.ModuleDescriptors.Model;

internal sealed record DefaultDescriptionObjectComponent(ImmutableArray<IDescriptionPropertyComponent> Properties, ImmutableArray<string> Hints) : IDescriptionObjectComponent
{
    public ValueTask AcceptAsync(IDescriptionComponentWriter writer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writer);
        return writer.WriteAsync(this, cancellationToken);
    }
}
