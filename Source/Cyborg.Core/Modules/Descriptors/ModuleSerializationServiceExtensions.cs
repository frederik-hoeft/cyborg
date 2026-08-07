using Cyborg.Core.Modules.Descriptors.Writers;

namespace Cyborg.Core.Modules.Descriptors;

public static class ModuleSerializationServiceExtensions
{
    extension (IModuleSerializationService self)
    {
        public ValueTask<string> ToTextAsync(IModuleDescriptor descriptor, CancellationToken cancellationToken = default) =>
            self.SerializeAsync(descriptor, TextModuleDescriptionSerializer.Instance, cancellationToken);

        public ValueTask<string> ToJsonAsync(IModuleDescriptor descriptor, bool indented = true, CancellationToken cancellationToken = default) =>
            self.SerializeAsync(descriptor, new JsonModuleDescriptionSerializer(indented), cancellationToken);
    }
}
