using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Cli.Arguments;

internal static class DynamicArgumentDefinitionParser
{
    private const char TYPE_DELIMITER = ':';

    public static bool TryParse(string definition, out DynamicArgumentDefinition result, [NotNullWhen(false)] out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(definition);

        int assignmentDelimiter = definition.IndexOf('=');
        if (assignmentDelimiter <= 0)
        {
            result = default;
            errorMessage = "Expected format 'key[:type]=value'.";
            return false;
        }

        string keyAndType = definition[..assignmentDelimiter];
        string value = definition[(assignmentDelimiter + 1)..];
        int typeDelimiter = keyAndType.IndexOf(TYPE_DELIMITER);
        string key;
        string? typeName;
        if (typeDelimiter < 0)
        {
            key = keyAndType;
            typeName = null;
        }
        else
        {
            if (typeDelimiter != keyAndType.LastIndexOf(TYPE_DELIMITER))
            {
                result = default;
                errorMessage = "Definitions may contain at most one type delimiter ':' before the assignment.";
                return false;
            }
            key = keyAndType[..typeDelimiter];
            typeName = keyAndType[(typeDelimiter + 1)..];
            if (string.IsNullOrWhiteSpace(typeName))
            {
                result = default;
                errorMessage = "Dynamic value type must not be empty.";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            result = default;
            errorMessage = "Key must not be empty.";
            return false;
        }

        result = new DynamicArgumentDefinition(key, typeName, value);
        errorMessage = null;
        return true;
    }
}
