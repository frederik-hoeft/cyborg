using Cyborg.Core.Common.Text;
using Cyborg.Core.Modules.Descriptors.Model;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Cyborg.Core.Modules.Descriptors.Writers;

public sealed class TextModuleDescriptionComponentWriter(
    IndentedStringBuilder builder) : IDescriptionComponentWriter
{
    public ValueTask WriteAtomAsync<T>(
        T value,
        ImmutableArray<string> hints,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WriteAtom(builder.GetInnerBuilder(), value);
        builder.GetInnerBuilder().AppendLine();
        return ValueTask.CompletedTask;
    }

    public async ValueTask WriteAsync(
        IDescriptionObjectComponent objectComponent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objectComponent);
        cancellationToken.ThrowIfCancellationRequested();

        if (objectComponent.Properties.IsEmpty)
        {
            builder.AppendLine("(empty)");
            return;
        }

        foreach (IDescriptionPropertyComponent property in objectComponent.Properties)
        {
            await property.AcceptAsync(this, cancellationToken);
        }
    }

    public async ValueTask WriteAsync(
        IDescriptionCollectionComponent collectionComponent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collectionComponent);
        cancellationToken.ThrowIfCancellationRequested();

        if (collectionComponent.Items.IsEmpty)
        {
            builder.AppendLine("(empty)");
            return;
        }

        for (int index = 0; index < collectionComponent.Items.Length; index++)
        {
            IDescriptionValueComponent item = collectionComponent.Items[index];
            builder.Append($"[{index}]:");

            if (item is IDescriptionObjectComponent or IDescriptionCollectionComponent)
            {
                builder.GetInnerBuilder().AppendLine();
                TextModuleDescriptionComponentWriter nestedWriter =
                    new(builder.IncreaseIndent());
                await item.AcceptAsync(nestedWriter, cancellationToken);
            }
            else
            {
                builder.GetInnerBuilder().Append(' ');
                await item.AcceptAsync(this, cancellationToken);
            }
        }
    }

    public ValueTask WriteAsync(
        IDescriptionValueComponent valueComponent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(valueComponent);
        cancellationToken.ThrowIfCancellationRequested();
        return valueComponent.AcceptAsync(this, cancellationToken);
    }

    public async ValueTask WriteAsync(
        IDescriptionPropertyComponent propertyComponent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(propertyComponent);
        cancellationToken.ThrowIfCancellationRequested();

        builder.Append($"{propertyComponent.Name}:");

        if (propertyComponent.Value is
            IDescriptionObjectComponent or IDescriptionCollectionComponent)
        {
            builder.GetInnerBuilder().AppendLine();
            TextModuleDescriptionComponentWriter nestedWriter =
                new(builder.IncreaseIndent());
            await propertyComponent.Value.AcceptAsync(
                nestedWriter,
                cancellationToken);
        }
        else
        {
            builder.GetInnerBuilder().Append(' ');
            await propertyComponent.Value.AcceptAsync(this, cancellationToken);
        }
    }

    private static void WriteAtom<T>(StringBuilder builder, T value)
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
                builder.Append(value.GetType().Name)
                    .Append('.')
                    .Append(value);
                break;
            default:
                builder.Append(
                    Convert.ToString(value, CultureInfo.InvariantCulture)
                    ?? value.GetType().Name);
                break;
        }
    }
}
