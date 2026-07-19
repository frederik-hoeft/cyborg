using Cyborg.Core.Modules.Descriptors.Model;
using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Descriptors.Writers;

// format-specific writer for module description components (JSON, plain text, etc.)
public interface IDescriptionComponentWriter
{
    // which atoms are supported depends on the format (e.g., JSON supports string, number, boolean, null, but not DateTime)
    ValueTask WriteAtomAsync<T>(T value, ImmutableArray<string> hints, CancellationToken cancellationToken);

    ValueTask WriteAsync(IDescriptionObjectComponent objectComponent, CancellationToken cancellationToken);

    ValueTask WriteAsync(IDescriptionCollectionComponent collectionComponent, CancellationToken cancellationToken);

    ValueTask WriteAsync(IDescriptionValueComponent valueComponent, CancellationToken cancellationToken);

    ValueTask WriteAsync(IDescriptionPropertyComponent propertyComponent, CancellationToken cancellationToken);
}
