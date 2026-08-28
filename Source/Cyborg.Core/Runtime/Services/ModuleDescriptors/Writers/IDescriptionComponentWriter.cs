using Cyborg.Core.Runtime.Services.ModuleDescriptors.Model;
using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Services.ModuleDescriptors.Writers;

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
