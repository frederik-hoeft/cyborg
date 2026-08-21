using Cyborg.Core.Configuration.Builders;
using Cyborg.Core.Configuration.Serialization;
using Cyborg.Core.Configuration.Serialization.Dynamics;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Cli.Arguments;

internal sealed class ConfigurationArgumentHandler
(
    IDynamicValueProviderRegistry providerRegistry,
    IJsonLoaderContext jsonLoaderContext
) : IConfigurationArgumentHandler
{
    public bool TryProcessArgument(
        string[]? configurationEntries,
        IConfigurationBuilder configurationBuilder,
        [NotNullWhen(false)] out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        errorMessage = null;
        if (configurationEntries is not [_, ..])
        {
            return true;
        }

        Dictionary<string, object> values = [];
        foreach (string definition in configurationEntries)
        {
            if (!DynamicArgumentDefinitionParser.TryParse(definition, out DynamicArgumentDefinition parsed, out string? parseError))
            {
                errorMessage = $"'{definition}'. Reason: {parseError}";
                return false;
            }

            object? value;
            if (parsed.TypeName is null)
            {
                value = parsed.Value;
            }
            else if (!TryParseDynamicValue(parsed.TypeName, parsed.Value, out value, out string? dynamicValueError))
            {
                errorMessage = $"'{definition}'. Reason: {dynamicValueError}";
                return false;
            }
            values[parsed.Key] = value;
        }

        configurationBuilder.AddDictionary(values);
        return true;
    }

    private bool TryParseDynamicValue(string typeName, string valueText, [NotNullWhen(true)] out object? value, [NotNullWhen(false)] out string? errorMessage)
    {
        if (!providerRegistry.TryGetProvider(typeName, out IDynamicValueProvider? provider))
        {
            value = null;
            errorMessage = $"Unknown dynamic value type '{typeName}'.";
            return false;
        }

        if (!DynamicArgumentValueParser.TryParse(provider, jsonLoaderContext, valueText, out value, out string? parseError))
        {
            errorMessage = $"Value is not valid for dynamic value type '{typeName}': {parseError}";
            return false;
        }
        errorMessage = null;
        return true;
    }
}
