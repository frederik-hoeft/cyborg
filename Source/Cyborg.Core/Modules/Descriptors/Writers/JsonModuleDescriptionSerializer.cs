using Cyborg.Core.Modules.Descriptors.Model;
using System.Text;
using System.Text.Json;

namespace Cyborg.Core.Modules.Descriptors.Writers;

public sealed class JsonModuleDescriptionSerializer(bool indented) : IModuleDescriptionSerializer
{
    public JsonModuleDescriptionSerializer() : this(indented: true)
    {
    }

    public string Format => ModuleDescriptionFormats.Json;

    public async ValueTask<string> SerializeAsync(IDescriptionObjectComponent description, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(description);
        cancellationToken.ThrowIfCancellationRequested();

        using MemoryStream stream = new();
        using Utf8JsonWriter jsonWriter = new(stream, new JsonWriterOptions { Indented = indented });
        JsonModuleDescriptionComponentWriter writer = new(jsonWriter);
        await description.AcceptAsync(writer, cancellationToken).ConfigureAwait(false);
        await jsonWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(stream.GetBuffer(), index: 0, count: checked((int)stream.Length));
    }
}
