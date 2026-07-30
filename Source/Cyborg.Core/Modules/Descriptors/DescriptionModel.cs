using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Modules.Descriptors.Builders;
using Cyborg.Core.Modules.Descriptors.Model;
using Cyborg.Core.Modules.Descriptors.Writers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Cyborg.Core.Modules.Descriptors
{
/// <summary>
/// Describes an object's inspectable state without coupling it to a specific output format.
/// </summary>
public interface IModuleDescriptor
{
    void Describe(IObjectDescriptionBuilder builder);
}

/// <summary>
/// Builds and renders format-neutral module descriptions.
/// </summary>
public static class ModuleDescription
{
    public static IDescriptionObjectComponent Build(IModuleDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        ObjectDescriptionBuilder builder = new(new DefaultDescriptionComponentFactory());
        descriptor.Describe(builder);
        return (IDescriptionObjectComponent)builder.Build();
    }

    public static string ToText(IModuleDescriptor descriptor)
        => TextModuleDescriptionWriter.Write(Build(descriptor));

    public static string ToJson(IModuleDescriptor descriptor, bool indented = true)
        => JsonModuleDescriptionWriter.Write(Build(descriptor), indented);
}
}

namespace Cyborg.Core.Modules.Descriptors.Model
{
public interface IDescriptionComponent
{
    ImmutableArray<string> Hints { get; }
}

public interface IDescriptionValueComponent : IDescriptionComponent
{
}

public interface IDescriptionPropertyComponent : IDescriptionComponent
{
    string Name { get; }

    IDescriptionValueComponent Value { get; }
}

public interface IDescriptionObjectComponent : IDescriptionValueComponent
{
    ImmutableArray<IDescriptionPropertyComponent> Properties { get; }
}

public interface IDescriptionCollectionComponent : IDescriptionValueComponent
{
    ImmutableArray<IDescriptionValueComponent> Items { get; }
}

public interface IDescriptionAtomComponent : IDescriptionValueComponent
{
    object? Value { get; }
}

public sealed record DescriptionPropertyComponent(
    string Name,
    IDescriptionValueComponent Value,
    ImmutableArray<string> Hints) : IDescriptionPropertyComponent;

public sealed record DescriptionObjectComponent(
    ImmutableArray<IDescriptionPropertyComponent> Properties,
    ImmutableArray<string> Hints) : IDescriptionObjectComponent;

public sealed record DescriptionCollectionComponent(
    ImmutableArray<IDescriptionValueComponent> Items,
    ImmutableArray<string> Hints) : IDescriptionCollectionComponent;

public sealed record DescriptionAtomComponent<T>(
    T TypedValue,
    ImmutableArray<string> Hints) : IDescriptionAtomComponent
{
    public object? Value => TypedValue;
}

public interface IDescriptionComponentFactory
{
    IDescriptionPropertyComponent CreateProperty(
        string name,
        IDescriptionValueComponent value,
        ImmutableArray<string> hints);

    IDescriptionValueComponent CreateValue<T>(T value, ImmutableArray<string> hints);

    IDescriptionObjectComponent CreateObject(
        ImmutableArray<IDescriptionPropertyComponent> properties,
        ImmutableArray<string> hints);

    IDescriptionCollectionComponent CreateCollection(
        ImmutableArray<IDescriptionValueComponent> items,
        ImmutableArray<string> hints);
}

public sealed class DefaultDescriptionComponentFactory : IDescriptionComponentFactory
{
    public IDescriptionPropertyComponent CreateProperty(
        string name,
        IDescriptionValueComponent value,
        ImmutableArray<string> hints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        return new DescriptionPropertyComponent(name, value, Normalize(hints));
    }

    public IDescriptionValueComponent CreateValue<T>(T value, ImmutableArray<string> hints)
        => new DescriptionAtomComponent<T>(value, Normalize(hints));

    public IDescriptionObjectComponent CreateObject(
        ImmutableArray<IDescriptionPropertyComponent> properties,
        ImmutableArray<string> hints)
        => new DescriptionObjectComponent(Normalize(properties), Normalize(hints));

    public IDescriptionCollectionComponent CreateCollection(
        ImmutableArray<IDescriptionValueComponent> items,
        ImmutableArray<string> hints)
        => new DescriptionCollectionComponent(Normalize(items), Normalize(hints));

    private static ImmutableArray<T> Normalize<T>(ImmutableArray<T> values)
        => values.IsDefault ? [] : values;
}
}

namespace Cyborg.Core.Modules.Descriptors.Builders
{
public interface IDescriptionBuilder
{
    IDescriptionComponent Build();
}

/// <summary>
/// Builds an object description from named properties.
/// </summary>
[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.IObjectDescriptionBuilder)]
public interface IObjectDescriptionBuilder : IDescriptionBuilder
{
    void AddProperty<T>(string name, ImmutableArray<string> hints, T value);

    void AddObject(string name, ImmutableArray<string> hints, Action<IObjectDescriptionBuilder> describe);

    void AddCollection(string name, ImmutableArray<string> hints, Action<ICollectionDescriptionBuilder> describe);
}

/// <summary>
/// Builds a collection description from ordered values.
/// </summary>
public interface ICollectionDescriptionBuilder : IDescriptionBuilder
{
    void AddItem<T>(ImmutableArray<string> hints, T item);

    void AddObjectItem(ImmutableArray<string> hints, Action<IObjectDescriptionBuilder> describe);

    void AddCollectionItem(ImmutableArray<string> hints, Action<ICollectionDescriptionBuilder> describe);
}

public sealed class ObjectDescriptionBuilder(IDescriptionComponentFactory factory) : IObjectDescriptionBuilder
{
    private readonly ImmutableArray<IDescriptionPropertyComponent>.Builder _properties =
        ImmutableArray.CreateBuilder<IDescriptionPropertyComponent>();

    private IDescriptionObjectComponent? _builtComponent;

    public IDescriptionComponent Build() => BuildComponent();

    public void AddProperty<T>(string name, ImmutableArray<string> hints, T value)
    {
        EnsureMutable();
        ValidateName(name);
        AddPropertyComponent(name, hints, factory.CreateValue(value, []));
    }

    public void AddObject(
        string name,
        ImmutableArray<string> hints,
        Action<IObjectDescriptionBuilder> describe)
    {
        EnsureMutable();
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(describe);

        ObjectDescriptionBuilder childBuilder = new(factory);
        describe(childBuilder);
        AddPropertyComponent(name, hints, childBuilder.BuildComponent());
    }

    public void AddCollection(
        string name,
        ImmutableArray<string> hints,
        Action<ICollectionDescriptionBuilder> describe)
    {
        EnsureMutable();
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(describe);

        CollectionDescriptionBuilder childBuilder = new(factory);
        describe(childBuilder);
        AddPropertyComponent(name, hints, childBuilder.BuildComponent());
    }

    internal IDescriptionObjectComponent BuildComponent(ImmutableArray<string> hints = default)
        => _builtComponent ??= factory.CreateObject(_properties.ToImmutable(), Normalize(hints));

    private void AddPropertyComponent(
        string name,
        ImmutableArray<string> hints,
        IDescriptionValueComponent value)
    {
        IDescriptionPropertyComponent property =
            factory.CreateProperty(name, value, Normalize(hints));
        _properties.Add(property);
    }

    private void EnsureMutable()
    {
        if (_builtComponent is not null)
        {
            throw new InvalidOperationException(
                "The description has already been built and can no longer be modified.");
        }
    }

    private static void ValidateName(string name)
        => ArgumentException.ThrowIfNullOrWhiteSpace(name);

    private static ImmutableArray<string> Normalize(ImmutableArray<string> hints)
        => hints.IsDefault ? [] : hints;
}

public sealed class CollectionDescriptionBuilder(IDescriptionComponentFactory factory) : ICollectionDescriptionBuilder
{
    private readonly ImmutableArray<IDescriptionValueComponent>.Builder _items =
        ImmutableArray.CreateBuilder<IDescriptionValueComponent>();

    private IDescriptionCollectionComponent? _builtComponent;

    public IDescriptionComponent Build() => BuildComponent();

    public void AddItem<T>(ImmutableArray<string> hints, T item)
    {
        EnsureMutable();
        _items.Add(factory.CreateValue(item, Normalize(hints)));
    }

    public void AddObjectItem(
        ImmutableArray<string> hints,
        Action<IObjectDescriptionBuilder> describe)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(describe);

        ObjectDescriptionBuilder childBuilder = new(factory);
        describe(childBuilder);
        _items.Add(childBuilder.BuildComponent(Normalize(hints)));
    }

    public void AddCollectionItem(
        ImmutableArray<string> hints,
        Action<ICollectionDescriptionBuilder> describe)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(describe);

        CollectionDescriptionBuilder childBuilder = new(factory);
        describe(childBuilder);
        _items.Add(childBuilder.BuildComponent(Normalize(hints)));
    }

    internal IDescriptionCollectionComponent BuildComponent(ImmutableArray<string> hints = default)
        => _builtComponent ??= factory.CreateCollection(_items.ToImmutable(), Normalize(hints));

    private void EnsureMutable()
    {
        if (_builtComponent is not null)
        {
            throw new InvalidOperationException(
                "The description has already been built and can no longer be modified.");
        }
    }

    private static ImmutableArray<string> Normalize(ImmutableArray<string> hints)
        => hints.IsDefault ? [] : hints;
}
}

namespace Cyborg.Core.Modules.Descriptors.Writers
{
public static class TextModuleDescriptionWriter
{
    private const int INDENT_SIZE = 2;

    public static string Write(IDescriptionObjectComponent description)
    {
        ArgumentNullException.ThrowIfNull(description);

        StringBuilder builder = new();
        WriteObject(builder, description, indentLevel: 0);
        return builder.ToString();
    }

    private static void WriteObject(
        StringBuilder builder,
        IDescriptionObjectComponent description,
        int indentLevel)
    {
        foreach (IDescriptionPropertyComponent property in description.Properties)
        {
            AppendIndent(builder, indentLevel);
            builder.Append(property.Name).Append(':');
            WritePropertyValue(builder, property.Value, indentLevel);
        }
    }

    private static void WritePropertyValue(
        StringBuilder builder,
        IDescriptionValueComponent value,
        int indentLevel)
    {
        if (value is IDescriptionAtomComponent atom)
        {
            builder.Append(' ');
            WriteAtom(builder, atom.Value);
            builder.AppendLine();
            return;
        }

        builder.AppendLine();
        WriteValue(builder, value, indentLevel + 1);
    }

    private static void WriteValue(
        StringBuilder builder,
        IDescriptionValueComponent value,
        int indentLevel)
    {
        switch (value)
        {
            case IDescriptionAtomComponent atom:
                AppendIndent(builder, indentLevel);
                WriteAtom(builder, atom.Value);
                builder.AppendLine();
                break;
            case IDescriptionObjectComponent objectComponent:
                WriteObject(builder, objectComponent, indentLevel);
                break;
            case IDescriptionCollectionComponent collection:
                WriteCollection(builder, collection, indentLevel);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported description component type '{value.GetType().FullName}'.");
        }
    }

    private static void WriteCollection(
        StringBuilder builder,
        IDescriptionCollectionComponent collection,
        int indentLevel)
    {
        if (collection.Items.IsEmpty)
        {
            AppendIndent(builder, indentLevel);
            builder.AppendLine("(empty)");
            return;
        }

        for (int index = 0; index < collection.Items.Length; index++)
        {
            IDescriptionValueComponent item = collection.Items[index];
            AppendIndent(builder, indentLevel);
            builder.Append('[').Append(index).Append("]:");

            if (item is IDescriptionAtomComponent atom)
            {
                builder.Append(' ');
                WriteAtom(builder, atom.Value);
                builder.AppendLine();
            }
            else
            {
                builder.AppendLine();
                WriteValue(builder, item, indentLevel + 1);
            }
        }
    }

    private static void WriteAtom(StringBuilder builder, object? value)
    {
        switch (value)
        {
            case null:
                builder.Append("null");
                break;
            case string text:
                builder.Append('"')
                    .Append(text.Replace("\\", "\\\\", StringComparison.Ordinal)
                        .Replace("\"", "\\\"", StringComparison.Ordinal))
                    .Append('"');
                break;
            case char character:
                builder.Append('\'').Append(character).Append('\'');
                break;
            case bool flag:
                builder.Append(flag ? "true" : "false");
                break;
            case Enum:
                builder.Append(value.GetType().Name).Append('.').Append(value);
                break;
            default:
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture)
                    ?? value.GetType().Name);
                break;
        }
    }

    private static void AppendIndent(StringBuilder builder, int indentLevel)
    {
        if (indentLevel > 0)
        {
            builder.Append(' ', indentLevel * INDENT_SIZE);
        }
    }
}

public static class JsonModuleDescriptionWriter
{
    public static string Write(
        IDescriptionObjectComponent description,
        bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(description);

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(
            stream,
            new JsonWriterOptions { Indented = indented }))
        {
            WriteObject(writer, description);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteObject(
        Utf8JsonWriter writer,
        IDescriptionObjectComponent description)
    {
        writer.WriteStartObject();
        foreach (IDescriptionPropertyComponent property in description.Properties)
        {
            writer.WritePropertyName(property.Name);
            WriteValue(writer, property.Value);
        }
        writer.WriteEndObject();
    }

    private static void WriteCollection(
        Utf8JsonWriter writer,
        IDescriptionCollectionComponent collection)
    {
        writer.WriteStartArray();
        foreach (IDescriptionValueComponent item in collection.Items)
        {
            WriteValue(writer, item);
        }
        writer.WriteEndArray();
    }

    private static void WriteValue(
        Utf8JsonWriter writer,
        IDescriptionValueComponent value)
    {
        switch (value)
        {
            case IDescriptionAtomComponent atom:
                WriteAtom(writer, atom.Value);
                break;
            case IDescriptionObjectComponent objectComponent:
                WriteObject(writer, objectComponent);
                break;
            case IDescriptionCollectionComponent collection:
                WriteCollection(writer, collection);
                break;
            default:
                throw new JsonException(
                    $"Unsupported description component type '{value.GetType().FullName}'.");
        }
    }

    private static void WriteAtom(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case bool boolean:
                writer.WriteBooleanValue(boolean);
                break;
            case byte number:
                writer.WriteNumberValue(number);
                break;
            case sbyte number:
                writer.WriteNumberValue(number);
                break;
            case short number:
                writer.WriteNumberValue(number);
                break;
            case ushort number:
                writer.WriteNumberValue(number);
                break;
            case int number:
                writer.WriteNumberValue(number);
                break;
            case uint number:
                writer.WriteNumberValue(number);
                break;
            case long number:
                writer.WriteNumberValue(number);
                break;
            case ulong number:
                writer.WriteNumberValue(number);
                break;
            case float number:
                writer.WriteNumberValue(number);
                break;
            case double number:
                writer.WriteNumberValue(number);
                break;
            case decimal number:
                writer.WriteNumberValue(number);
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case char character:
                writer.WriteStringValue(character.ToString());
                break;
            case DateTime dateTime:
                writer.WriteStringValue(dateTime);
                break;
            case DateTimeOffset dateTimeOffset:
                writer.WriteStringValue(dateTimeOffset);
                break;
            case Guid guid:
                writer.WriteStringValue(guid);
                break;
            case TimeSpan timeSpan:
                writer.WriteStringValue(timeSpan.ToString("c", CultureInfo.InvariantCulture));
                break;
            case Enum enumValue:
                writer.WriteStringValue($"{enumValue.GetType().Name}.{enumValue}");
                break;
            default:
                writer.WriteStringValue(
                    Convert.ToString(value, CultureInfo.InvariantCulture)
                    ?? value.GetType().Name);
                break;
        }
    }
}
}
