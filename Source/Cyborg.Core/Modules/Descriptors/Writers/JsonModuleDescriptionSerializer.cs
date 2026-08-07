using Cyborg.Core.Modules.Descriptors.Model;
using System.Text;
using System.Text.Json;

namespace Cyborg.Core.Modules.Descriptors.Writers;

internal sealed class JsonModuleDescriptionSerializer : IModuleDescriptionSerializer
{
    private readonly bool _indented;

    internal JsonModuleDescriptionSerializer()
        : this(indented: true)
    {
    }

    internal JsonModuleDescriptionSerializer(bool indented)
    {
        _indented = indented;
    }

    public string Format => ModuleDescriptionFormats.JSON;

    public async ValueTask<string> SerializeAsync(
        IDescriptionObjectComponent description,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(description);
        cancellationToken.ThrowIfCancellationRequested();

        using MemoryStream stream = new();
        using Utf8JsonWriter jsonWriter = new(
            stream,
            new JsonWriterOptions { Indented = _indented });
        JsonModuleDescriptionComponentWriter writer = new(jsonWriter);
        await description.AcceptAsync(writer, cancellationToken).ConfigureAwait(false);
        await jsonWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(
            stream.GetBuffer(),
            0,
            checked((int)stream.Length));
    }
}
