using Cyborg.Core.Runtime.Services.ModuleDescriptors.Model;

namespace Cyborg.Core.Runtime.Services.ModuleDescriptors;

public interface IModuleSerializationService
{
    ValueTask<IDescriptionObjectComponent> BuildAsync(IModuleDescriptor descriptor, CancellationToken cancellationToken = default);

    ValueTask<string> SerializeAsync(IModuleDescriptor descriptor, IModuleDescriptionSerializer serializer, CancellationToken cancellationToken = default);

    ValueTask<string> SerializeAsync(IModuleDescriptor descriptor, string format, CancellationToken cancellationToken = default);
}
