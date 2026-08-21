using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Configuration.Serialization;
using Cyborg.Core.Configuration.Serialization.Dynamics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace Cyborg.Cli.Arguments;

internal static class DynamicArgumentValueParser
{
    internal static bool TryParse(
        IDynamicValueProvider provider,
        IJsonLoaderContext jsonLoaderContext,
        string valueText,
        [NotNullWhen(true)] out object? value,
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(jsonLoaderContext);
        ArgumentNullException.ThrowIfNull(valueText);

        try
        {
            Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(valueText));
            if (!reader.Read() || !provider.TryCreateValue(ref reader, jsonLoaderContext, out DynamicValue? dynamicValue) || reader.Read())
            {
                value = null;
                errorMessage = "Expected exactly one valid JSON value.";
                return false;
            }
            value = dynamicValue.Value;
            errorMessage = null;
            return true;
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException or NotSupportedException)
        {
            value = null;
            errorMessage = exception.Message;
            return false;
        }
    }
}
