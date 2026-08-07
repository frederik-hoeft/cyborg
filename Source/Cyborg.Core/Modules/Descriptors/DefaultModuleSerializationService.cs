using Cyborg.Core.Modules.Descriptors.Builders;
using Cyborg.Core.Modules.Descriptors.Model;

namespace Cyborg.Core.Modules.Descriptors;

public sealed class DefaultModuleSerializationService(IObjectDescriptionBuilderFactory builderFactory, IModuleDescriptionSerializerRegistry serializerRegistry) : IModuleSerializationService
{
    public async ValueTask<IDescriptionObjectComponent> BuildAsync(IModuleDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        cancellationToken.ThrowIfCancellationRequested();
        IObjectDescriptionBuilder builder = builderFactory.CreateBuilder();

        await descriptor.DescribeAsync(builder, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return builder.Build();
    }

    public async ValueTask<string> SerializeAsync(IModuleDescriptor descriptor, IModuleDescriptionSerializer serializer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serializer);

        IDescriptionObjectComponent description = await BuildAsync(descriptor, cancellationToken);
        return await serializer.SerializeAsync(description, cancellationToken);
    }

    public ValueTask<string> SerializeAsync(IModuleDescriptor descriptor, string format, CancellationToken cancellationToken = default)
    {
        IModuleDescriptionSerializer serializer = serializerRegistry.GetRequiredSerializer(format);
        return SerializeAsync(descriptor, serializer, cancellationToken);
    }
}
