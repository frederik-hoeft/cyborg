using Cyborg.Core.Configuration.Model;
using System.Text.Json;

namespace Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

public abstract class DynamicValueProviderPrimitives<T>(string typeName) : IDynamicValueProvider
{
    public string TypeName => typeName;

    public bool TryCreateValue(ref Utf8JsonReader reader, IJsonLoaderContext context, [NotNullWhen(true)] out DynamicValue? value) => (value = 0 switch
    {
        _ when typeof(T) == typeof(sbyte) => new DynamicValue(reader.GetSByte()),
        _ when typeof(T) == typeof(byte) => new DynamicValue(reader.GetByte()),
        _ when typeof(T) == typeof(short) => new DynamicValue(reader.GetInt16()),
        _ when typeof(T) == typeof(ushort) => new DynamicValue(reader.GetUInt16()),
        _ when typeof(T) == typeof(int) => new DynamicValue(reader.GetInt32()),
        _ when typeof(T) == typeof(uint) => new DynamicValue(reader.GetUInt32()),
        _ when typeof(T) == typeof(long) => new DynamicValue(reader.GetInt64()),
        _ when typeof(T) == typeof(ulong) => new DynamicValue(reader.GetUInt64()),
        _ when typeof(T) == typeof(float) => new DynamicValue(reader.GetSingle()),
        _ when typeof(T) == typeof(double) => new DynamicValue(reader.GetDouble()),
        _ when typeof(T) == typeof(decimal) => new DynamicValue(reader.GetDecimal()),
        _ when typeof(T) == typeof(bool) => new DynamicValue(reader.GetBoolean()),
        _ when typeof(T) == typeof(string) => new DynamicValue(reader.GetString()!),
        _ => null
    }) is not null;
}