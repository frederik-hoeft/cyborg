using Cyborg.Core.Modules.Descriptors.Model;

namespace Cyborg.Core.Modules.Descriptors;

public interface IModuleSerializationService
{
    ValueTask<IDescriptionObjectComponent> BuildAsync(IModuleDescriptor descriptor, CancellationToken cancellationToken = default);

    ValueTask<string> SerializeAsync(IModuleDescriptor descriptor, IModuleDescriptionSerializer serializer, CancellationToken cancellationToken = default);

    ValueTask<string> SerializeAsync(IModuleDescriptor descriptor, string format, CancellationToken cancellationToken = default);
}
