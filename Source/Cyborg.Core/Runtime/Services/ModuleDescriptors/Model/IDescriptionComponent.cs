using Cyborg.Core.Runtime.Services.ModuleDescriptors.Writers;
using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Services.ModuleDescriptors.Model;

public interface IDescriptionComponent
{
    ImmutableArray<string> Hints { get; }

    ValueTask AcceptAsync(IDescriptionComponentWriter writer, CancellationToken cancellationToken);
}
