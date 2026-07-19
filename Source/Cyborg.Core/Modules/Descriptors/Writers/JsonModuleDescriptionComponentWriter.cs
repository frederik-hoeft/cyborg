using Cyborg.Core.Modules.Descriptors.Model;
using System.Collections.Immutable;
using System.Text.Json;

namespace Cyborg.Core.Modules.Descriptors.Writers;

public sealed class JsonModuleDescriptionComponentWriter(Utf8JsonWriter jsonWriter) : IDescriptionComponentWriter
{
    public ValueTask WriteAtomAsync<T>(T value, ImmutableArray<string> hints, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // hints are not supported for JSON, but could be used for formatting in other implementations
        return value switch
        {
            bool boolValue => Do(() => jsonWriter.WriteBooleanValue(boolValue)),
            sbyte sbyteValue => Do(() => jsonWriter.WriteNumberValue(sbyteValue)),
            byte byteValue => Do(() => jsonWriter.WriteNumberValue(byteValue)),
            short shortValue => Do(() => jsonWriter.WriteNumberValue(shortValue)),
            ushort ushortValue => Do(() => jsonWriter.WriteNumberValue(ushortValue)),
            int intValue => Do(() => jsonWriter.WriteNumberValue(intValue)),
            uint uintValue => Do(() => jsonWriter.WriteNumberValue(uintValue)),
            long longValue => Do(() => jsonWriter.WriteNumberValue(longValue)),
            ulong ulongValue => Do(() => jsonWriter.WriteNumberValue(ulongValue)),
            float floatValue => Do(() => jsonWriter.WriteNumberValue(floatValue)),
            double doubleValue => Do(() => jsonWriter.WriteNumberValue(doubleValue)),
            decimal decimalValue => Do(() => jsonWriter.WriteNumberValue(decimalValue)),
            string stringValue => Do(() => jsonWriter.WriteStringValue(stringValue)),
            DateTime dateTimeValue => Do(() => jsonWriter.WriteStringValue(dateTimeValue)),
            DateTimeOffset dateTimeOffsetValue => Do(() => jsonWriter.WriteStringValue(dateTimeOffsetValue)),
            Guid guidValue => Do(() => jsonWriter.WriteStringValue(guidValue)),
            TimeSpan timeSpanValue => Do(() => jsonWriter.WriteStringValue(timeSpanValue.ToString())),
            null => Do(jsonWriter.WriteNullValue),
            _ => throw new JsonException($"Unsupported atom type: {typeof(T).FullName}"),
        };
    }

    public async ValueTask WriteAsync(IDescriptionObjectComponent objectComponent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objectComponent);
        cancellationToken.ThrowIfCancellationRequested();

        jsonWriter.WriteStartObject();
        foreach (IDescriptionPropertyComponent property in objectComponent.Properties)
        {
            await property.AcceptAsync(this, cancellationToken);
        }
        jsonWriter.WriteEndObject();
    }

    public async ValueTask WriteAsync(IDescriptionCollectionComponent collectionComponent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collectionComponent);
        cancellationToken.ThrowIfCancellationRequested();

        jsonWriter.WriteStartArray();
        foreach (IDescriptionValueComponent item in collectionComponent.Items)
        {
            await item.AcceptAsync(this, cancellationToken);
        }
        jsonWriter.WriteEndArray();
    }

    public ValueTask WriteAsync(IDescriptionValueComponent valueComponent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(valueComponent);
        cancellationToken.ThrowIfCancellationRequested();
        // TODO: check well-known metadata hints for special handling (e.g., "secret", "hidden", etc.), not currently used (attributes are on properties, not values)
        return valueComponent.AcceptAsync(this, cancellationToken);
    }

    public async ValueTask WriteAsync(IDescriptionPropertyComponent propertyComponent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(propertyComponent);
        cancellationToken.ThrowIfCancellationRequested();
        // TODO: check well-known metadata hints for special handling (e.g., "secret", "hidden", etc.)
        jsonWriter.WritePropertyName(propertyComponent.Name);
        await propertyComponent.Value.AcceptAsync(this, cancellationToken);
    }

    private static ValueTask Do(Action action)
    {
        action();
        return ValueTask.CompletedTask;
    }
}
