namespace Cyborg.Core.Runtime.Services.ModuleDescriptors;

public static class ModuleSerializationServiceExtensions
{
    public static ValueTask<string> ToTextAsync(this IModuleSerializationService serializationService, IModuleDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serializationService);
        return serializationService.SerializeAsync(descriptor, ModuleDescriptionFormats.Text, cancellationToken);
    }

    public static ValueTask<string> ToJsonAsync(this IModuleSerializationService serializationService, IModuleDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serializationService);
        return serializationService.SerializeAsync(descriptor, ModuleDescriptionFormats.Json, cancellationToken);
    }
}
