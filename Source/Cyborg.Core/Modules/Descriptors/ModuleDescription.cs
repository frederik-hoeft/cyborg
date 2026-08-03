using Cyborg.Core.Common.Text;
using Cyborg.Core.Modules.Descriptors.Builders;
using Cyborg.Core.Modules.Descriptors.Model;
using Cyborg.Core.Modules.Descriptors.Writers;
using System.Text;
using System.Text.Json;

namespace Cyborg.Core.Modules.Descriptors;

public static class ModuleDescription
{
    public static IDescriptionObjectComponent Build(IModuleDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        ObjectDescriptionBuilder builder = new(new DefaultDescriptionComponentFactory());
        descriptor.Describe(builder);
        return builder.BuildComponent();
    }

    public static string ToText(IModuleDescriptor descriptor)
    {
        IDescriptionObjectComponent description = Build(descriptor);
        StringBuilder builder = new();
        TextModuleDescriptionComponentWriter writer =
            new(new IndentedStringBuilder(builder));

        description.AcceptAsync(writer, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        return builder.ToString();
    }

    public static string ToJson(IModuleDescriptor descriptor, bool indented = true)
    {
        IDescriptionObjectComponent description = Build(descriptor);
        using MemoryStream stream = new();
        using Utf8JsonWriter jsonWriter = new(
            stream,
            new JsonWriterOptions { Indented = indented });

        JsonModuleDescriptionComponentWriter writer = new(jsonWriter);
        description.AcceptAsync(writer, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        jsonWriter.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
