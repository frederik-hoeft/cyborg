using Cyborg.Core.Modules.Descriptors.Model;
using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Descriptors.Writers;

public interface IDescriptionComponentWriter
{
    ValueTask WriteAtomAsync<T>(
        T value,
        ImmutableArray<string> hints,
        CancellationToken cancellationToken);

    ValueTask WriteAsync(
        IDescriptionObjectComponent objectComponent,
        CancellationToken cancellationToken);

    ValueTask WriteAsync(
        IDescriptionCollectionComponent collectionComponent,
        CancellationToken cancellationToken);

    ValueTask WriteAsync(
        IDescriptionValueComponent valueComponent,
        CancellationToken cancellationToken);

    ValueTask WriteAsync(
        IDescriptionPropertyComponent propertyComponent,
        CancellationToken cancellationToken);
}
