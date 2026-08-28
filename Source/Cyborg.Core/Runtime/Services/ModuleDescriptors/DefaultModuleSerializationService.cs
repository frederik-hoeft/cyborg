using Cyborg.Core.Runtime.Services.ModuleDescriptors.Builders;
using Cyborg.Core.Runtime.Services.ModuleDescriptors.Model;

namespace Cyborg.Core.Runtime.Services.ModuleDescriptors;

internal sealed class DefaultModuleSerializationService(IModuleDescriptionSerializerRegistry serializerRegistry) : IModuleSerializationService
{
    public async ValueTask<IDescriptionObjectComponent> BuildAsync(IModuleDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        cancellationToken.ThrowIfCancellationRequested();

        ObjectDescriptionBuilder builder = new(DefaultDescriptionComponentFactory.Instance);
        await descriptor.DescribeAsync(builder, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return builder.BuildComponent();
    }

    public async ValueTask<string> SerializeAsync(IModuleDescriptor descriptor, IModuleDescriptionSerializer serializer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serializer);

        IDescriptionObjectComponent description = await BuildAsync(descriptor, cancellationToken).ConfigureAwait(false);
        return await serializer.SerializeAsync(description, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<string> SerializeAsync(IModuleDescriptor descriptor, string format, CancellationToken cancellationToken = default)
    {
        IModuleDescriptionSerializer serializer = serializerRegistry.GetRequiredSerializer(format);
        return SerializeAsync(descriptor, serializer, cancellationToken);
    }
}
