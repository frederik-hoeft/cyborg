using Cyborg.Core.Modules.Descriptors.Model;
using Cyborg.Core.Text.Rendering;
using System.Text;
using System.Text.Json;

namespace Cyborg.Core.Modules.Descriptors.Writers;

internal sealed class JsonModuleDescriptionSerializer(bool indented, ITaggedStringRenderer taggedStringRenderer) : IModuleDescriptionSerializer
{
    public string Format => ModuleDescriptionFormats.Json;

    public async ValueTask<string> SerializeAsync(IDescriptionObjectComponent description, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(description);
        cancellationToken.ThrowIfCancellationRequested();

        using MemoryStream stream = new();
        using Utf8JsonWriter jsonWriter = new(stream, new JsonWriterOptions { Indented = indented });
        JsonModuleDescriptionComponentWriter writer = new(jsonWriter, taggedStringRenderer);
        await description.AcceptAsync(writer, cancellationToken).ConfigureAwait(false);
        await jsonWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(stream.GetBuffer(), index: 0, count: checked((int)stream.Length));
    }
}
