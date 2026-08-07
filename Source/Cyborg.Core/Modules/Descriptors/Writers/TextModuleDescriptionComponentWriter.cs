using Cyborg.Core.Common.Text;
using Cyborg.Core.Modules.Descriptors.Model;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Cyborg.Core.Modules.Descriptors.Writers;

internal sealed class TextModuleDescriptionComponentWriter(IndentedStringBuilder builder) : IDescriptionComponentWriter
{
    public ValueTask WriteAtomAsync<T>(T value, ImmutableArray<string> hints, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WriteAtom(builder.GetInnerBuilder(), value);
        builder.GetInnerBuilder().AppendLine();
        return ValueTask.CompletedTask;
    }

    public async ValueTask WriteAsync(IDescriptionObjectComponent objectComponent, CancellationToken cancellationToken)
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
            await property.AcceptAsync(this, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask WriteAsync(IDescriptionCollectionComponent collectionComponent, CancellationToken cancellationToken)
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
                TextModuleDescriptionComponentWriter nestedWriter = new(builder.IncreaseIndent());
                await item.AcceptAsync(nestedWriter, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                builder.GetInnerBuilder().Append(' ');
                await item.AcceptAsync(this, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask WriteAsync(IDescriptionPropertyComponent propertyComponent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(propertyComponent);
        cancellationToken.ThrowIfCancellationRequested();

        builder.Append($"{propertyComponent.Name}:");

        if (propertyComponent.Value is IDescriptionObjectComponent or IDescriptionCollectionComponent)
        {
            builder.GetInnerBuilder().AppendLine();
            TextModuleDescriptionComponentWriter nestedWriter = new(builder.IncreaseIndent());
            await propertyComponent.Value.AcceptAsync(nestedWriter, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            builder.GetInnerBuilder().Append(' ');
            await propertyComponent.Value.AcceptAsync(this, cancellationToken).ConfigureAwait(false);
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
                AppendQuotedString(builder, text, '"');
                break;
            case char character:
                AppendQuotedString(builder, character.ToString(), '\'');
                break;
            case bool flag:
                builder.Append(flag ? "true" : "false");
                break;
            case DateTime dateTime:
                builder.Append(dateTime.ToString("O", CultureInfo.InvariantCulture));
                break;
            case DateTimeOffset dateTimeOffset:
                builder.Append(dateTimeOffset.ToString("O", CultureInfo.InvariantCulture));
                break;
            case TimeSpan timeSpan:
                builder.Append(timeSpan.ToString("c", CultureInfo.InvariantCulture));
                break;
            case Guid guid:
                builder.Append(guid.ToString("D"));
                break;
            case Enum:
                builder.Append(value.GetType().Name).Append('.').Append(value);
                break;
            default:
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.GetType().Name);
                break;
        }
    }

    private static void AppendQuotedString(StringBuilder builder, string value, char quote)
    {
        builder.Append(quote);
        foreach (char character in value)
        {
            switch (character)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"' when quote == '"':
                    builder.Append("\\\"");
                    break;
                case '\'' when quote == '\'':
                    builder.Append("\\'");
                    break;
                case '\0':
                    builder.Append("\\0");
                    break;
                case '\a':
                    builder.Append("\\a");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                case '\v':
                    builder.Append("\\v");
                    break;
                default:
                    if (char.IsControl(character))
                    {
                        builder.Append("\\u").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }
                    break;
            }
        }
        builder.Append(quote);
    }
}
