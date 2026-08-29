using Cyborg.Core.Runtime.Services.ModuleDescriptors.Writers;
using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Services.ModuleDescriptors.Model;

internal sealed record DefaultDescriptionValueComponent<T>(T Value, ImmutableArray<string> Hints) : IDescriptionValueComponent
{
    public ValueTask AcceptAsync(IDescriptionComponentWriter writer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writer);
        return writer.WriteAtomAsync(Value, Hints, cancellationToken);
    }
}
