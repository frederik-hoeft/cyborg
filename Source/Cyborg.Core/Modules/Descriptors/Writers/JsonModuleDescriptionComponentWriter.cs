using Cyborg.Core.Modules.Descriptors.Model;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace Cyborg.Core.Modules.Descriptors.Writers;

internal sealed class JsonModuleDescriptionComponentWriter(
    Utf8JsonWriter jsonWriter) : IDescriptionComponentWriter
{
    public ValueTask WriteAtomAsync<T>(
        T value,
        ImmutableArray<string> hints,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (value)
        {
            case null:
                jsonWriter.WriteNullValue();
                break;
            case bool boolValue:
                jsonWriter.WriteBooleanValue(boolValue);
                break;
            case sbyte number:
                jsonWriter.WriteNumberValue(number);
                break;
            case byte number:
                jsonWriter.WriteNumberValue(number);
                break;
            case short number:
                jsonWriter.WriteNumberValue(number);
                break;
            case ushort number:
                jsonWriter.WriteNumberValue(number);
                break;
            case int number:
                jsonWriter.WriteNumberValue(number);
                break;
            case uint number:
                jsonWriter.WriteNumberValue(number);
                break;
            case long number:
                jsonWriter.WriteNumberValue(number);
                break;
            case ulong number:
                jsonWriter.WriteNumberValue(number);
                break;
            case float number:
                jsonWriter.WriteNumberValue(number);
                break;
            case double number:
                jsonWriter.WriteNumberValue(number);
                break;
            case decimal number:
                jsonWriter.WriteNumberValue(number);
                break;
            case string text:
                jsonWriter.WriteStringValue(text);
                break;
            case char character:
                jsonWriter.WriteStringValue(character.ToString());
                break;
            case DateTime dateTime:
                jsonWriter.WriteStringValue(dateTime);
                break;
            case DateTimeOffset dateTimeOffset:
                jsonWriter.WriteStringValue(dateTimeOffset);
                break;
            case Guid guid:
                jsonWriter.WriteStringValue(guid);
                break;
            case TimeSpan timeSpan:
                jsonWriter.WriteStringValue(
                    timeSpan.ToString("c", CultureInfo.InvariantCulture));
                break;
            case Enum enumValue:
                jsonWriter.WriteStringValue(
                    $"{enumValue.GetType().Name}.{enumValue}");
                break;
            default:
                jsonWriter.WriteStringValue(
                    Convert.ToString(value, CultureInfo.InvariantCulture)
                    ?? value.GetType().Name);
                break;
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask WriteAsync(
        IDescriptionObjectComponent objectComponent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objectComponent);
        cancellationToken.ThrowIfCancellationRequested();

        jsonWriter.WriteStartObject();
        foreach (IDescriptionPropertyComponent property in objectComponent.Properties)
        {
            await property.AcceptAsync(this, cancellationToken).ConfigureAwait(false);
        }
        jsonWriter.WriteEndObject();
    }

    public async ValueTask WriteAsync(
        IDescriptionCollectionComponent collectionComponent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collectionComponent);
        cancellationToken.ThrowIfCancellationRequested();

        jsonWriter.WriteStartArray();
        foreach (IDescriptionValueComponent item in collectionComponent.Items)
        {
            await item.AcceptAsync(this, cancellationToken).ConfigureAwait(false);
        }
        jsonWriter.WriteEndArray();
    }

    public async ValueTask WriteAsync(
        IDescriptionPropertyComponent propertyComponent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(propertyComponent);
        cancellationToken.ThrowIfCancellationRequested();

        jsonWriter.WritePropertyName(propertyComponent.Name);
        await propertyComponent.Value.AcceptAsync(this, cancellationToken)
            .ConfigureAwait(false);
    }
}
