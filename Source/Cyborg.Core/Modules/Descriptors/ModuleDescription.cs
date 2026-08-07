using Cyborg.Core.Modules.Descriptors.Builders;
using Cyborg.Core.Modules.Descriptors.Model;
using Cyborg.Core.Modules.Descriptors.Writers;

namespace Cyborg.Core.Modules.Descriptors;

public static class ModuleDescription
{
    public static async ValueTask<IDescriptionObjectComponent> BuildAsync(
        IModuleDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        cancellationToken.ThrowIfCancellationRequested();

        ObjectDescriptionBuilder builder = new(new DefaultDescriptionComponentFactory());
        await descriptor.DescribeAsync(builder, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return builder.BuildComponent();
    }

    public static async ValueTask<string> SerializeAsync(
        IModuleDescriptor descriptor,
        IModuleDescriptionSerializer serializer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serializer);

        IDescriptionObjectComponent description =
            await BuildAsync(descriptor, cancellationToken).ConfigureAwait(false);
        return await serializer.SerializeAsync(description, cancellationToken)
            .ConfigureAwait(false);
    }

    public static ValueTask<string> ToTextAsync(
        IModuleDescriptor descriptor,
        CancellationToken cancellationToken = default)
        => SerializeAsync(
            descriptor,
            TextModuleDescriptionSerializer.Instance,
            cancellationToken);

    public static ValueTask<string> ToJsonAsync(
        IModuleDescriptor descriptor,
        bool indented = true,
        CancellationToken cancellationToken = default)
        => SerializeAsync(
            descriptor,
            new JsonModuleDescriptionSerializer(indented),
            cancellationToken);
}
