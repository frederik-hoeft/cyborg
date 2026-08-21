using Cyborg.Core.Configuration.Serialization;
using Cyborg.Core.Configuration.Serialization.Dynamics;
using Cyborg.Core.Text;
using Cyborg.Core.Text.Rendering;

namespace Cyborg.Cli.Arguments;

internal sealed class DynamicArgumentLogRenderer
(
    IDynamicValueProviderRegistry providerRegistry,
    IJsonLoaderContext jsonLoaderContext,
    ITaggedStringRenderer taggedStringRenderer
)
{
    public string RenderDefinition(string definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!DynamicArgumentDefinitionParser.TryParse(definition, out DynamicArgumentDefinition parsed, out _))
        {
            return "<invalid>";
        }
        if (parsed.TypeName is null)
        {
            return definition;
        }
        if (!providerRegistry.TryGetProvider(parsed.TypeName, out IDynamicValueProvider? provider)
            || !DynamicArgumentValueParser.TryParse(provider, jsonLoaderContext, parsed.Value, out object? value, out _))
        {
            return $"{parsed.Key}:{parsed.TypeName}=<invalid>";
        }

        string renderedValue = value switch
        {
            TaggedString taggedString => taggedStringRenderer.Render(taggedString),
            null => "null",
            string or char or bool or sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal or Enum => parsed.Value,
            _ => "<structured>",
        };
        return $"{parsed.Key}:{parsed.TypeName}={renderedValue}";
    }
}
