using Cyborg.Core.Modules.Descriptors.Writers;
using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Descriptors.Model;

public interface IDescriptionComponent
{
    ImmutableArray<string> Hints { get; }

    ValueTask AcceptAsync(
        IDescriptionComponentWriter writer,
        CancellationToken cancellationToken);
}
