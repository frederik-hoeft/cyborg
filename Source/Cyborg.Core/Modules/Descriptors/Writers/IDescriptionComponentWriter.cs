using Cyborg.Core.Modules.Descriptors.Model;
using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Descriptors.Writers;

/// <summary>
/// Visitor used by custom module-description serializers to consume the immutable description tree.
/// </summary>
public interface IDescriptionComponentWriter
{
    ValueTask WriteAtomAsync<T>(T value, ImmutableArray<string> hints, CancellationToken cancellationToken);

    ValueTask WriteAsync(IDescriptionObjectComponent objectComponent, CancellationToken cancellationToken);

    ValueTask WriteAsync(IDescriptionCollectionComponent collectionComponent, CancellationToken cancellationToken);

    ValueTask WriteAsync(IDescriptionPropertyComponent propertyComponent, CancellationToken cancellationToken);
}
