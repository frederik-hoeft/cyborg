using Cyborg.Core.Common.Text;
using Cyborg.Core.Modules.Descriptors.Model;
using System.Text;

namespace Cyborg.Core.Modules.Descriptors.Writers;

internal sealed class TextModuleDescriptionSerializer : IModuleDescriptionSerializer
{
    internal static TextModuleDescriptionSerializer Instance { get; } = new();

    public string Format => ModuleDescriptionFormats.Text;

    public async ValueTask<string> SerializeAsync(IDescriptionObjectComponent description, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(description);
        cancellationToken.ThrowIfCancellationRequested();

        StringBuilder builder = new();
        TextModuleDescriptionComponentWriter writer = new(new IndentedStringBuilder(builder));
        await description.AcceptAsync(writer, cancellationToken);
        return builder.ToString();
    }
}
