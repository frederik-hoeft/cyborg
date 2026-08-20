using Cyborg.Core.Common.Text;
using Cyborg.Core.Modules.Descriptors.Model;
using Cyborg.Core.Text.Rendering;
using System.Text;

namespace Cyborg.Core.Modules.Descriptors.Writers;

internal sealed class TextModuleDescriptionSerializer(ITaggedStringRenderer taggedStringRenderer) : IModuleDescriptionSerializer
{
    public string Format => ModuleDescriptionFormats.Text;

    public async ValueTask<string> SerializeAsync(IDescriptionObjectComponent description, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(description);
        cancellationToken.ThrowIfCancellationRequested();

        StringBuilder builder = new();
        TextModuleDescriptionComponentWriter writer = new(new IndentedStringBuilder(builder, indentSize: 2), taggedStringRenderer);
        await description.AcceptAsync(writer, cancellationToken);
        return builder.ToString();
    }
}
