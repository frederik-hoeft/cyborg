using Cyborg.Core.Modules.Descriptors.Writers;
using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Descriptors.Model;

// base for component metadata, which can be serialized to a stream
public interface IDescriptionComponent
{
    ImmutableArray<string> Hints { get; }

    ValueTask AcceptAsync(IDescriptionComponentWriter writer, CancellationToken cancellationToken);
}
