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
    private const string TYPE_DELIMITER = "::";

    public bool TryProcessArgument(
        string[]? configurationEntries,
        IConfigurationBuilder configurationBuilder,
        [NotNullWhen(false)] out string? invalidDefinition,
        [NotNullWhen(false)] out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        invalidDefinition = null;
        errorMessage = null;
        if (configurationEntries is not [_, ..])
        {
            return true;
        }

        Dictionary<string, object> values = [];
        foreach (string definition in configurationEntries)
        {
            if (!TryParseDefinition(definition, out string? key, out string? typeName, out string? valueText, out errorMessage))
            {
                invalidDefinition = definition;
                return false;
            }

            object value;
            if (typeName is null)
            {
                value = valueText;
            }
            else if (!TryParseDynamicValue(typeName, valueText, out value, out errorMessage))
            {
                invalidDefinition = definition;
                return false;
            }
            values[key] = value;
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

    private static bool TryParseDefinition(
        string definition,
        [NotNullWhen(true)] out string? key,
        out string? typeName,
        [NotNullWhen(true)] out string? value,
        [NotNullWhen(false)] out string? errorMessage)
    {
        int assignmentDelimiter = definition.IndexOf('=');
        if (assignmentDelimiter <= 0)
        {
            key = null;
            typeName = null;
            value = null;
            errorMessage = "Expected format 'key[::type]=value'.";
            return false;
        }

        string keyAndType = definition[..assignmentDelimiter];
        value = definition[(assignmentDelimiter + 1)..];
        int typeDelimiter = keyAndType.LastIndexOf(TYPE_DELIMITER, StringComparison.Ordinal);
        if (typeDelimiter < 0)
        {
            key = keyAndType;
            typeName = null;
        }
        else
        {
            key = keyAndType[..typeDelimiter];
            typeName = keyAndType[(typeDelimiter + TYPE_DELIMITER.Length)..];
            if (string.IsNullOrWhiteSpace(typeName))
            {
                errorMessage = "Dynamic value type must not be empty.";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            errorMessage = "Configuration key must not be empty.";
            return false;
        }

        errorMessage = null;
        return true;
    }
}
