using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Descriptors.Model;
using Cyborg.Core.Text;
using Cyborg.Core.Text.Rendering;
using Cyborg.Shared.Text;
using System.Collections.Immutable;
using System.Globalization;

namespace Cyborg.Core.Modules.Descriptors.Writers;

internal sealed class TextModuleDescriptionComponentWriter(IndentedStringBuilder builder, ITaggedStringRenderer taggedStringRenderer) : IDescriptionComponentWriter
{
    public ValueTask WriteAtomAsync<T>(T value, ImmutableArray<string> hints, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WriteAtom(builder, taggedStringRenderer, value, hints);
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
                builder.AppendLine();
                TextModuleDescriptionComponentWriter nestedWriter = new(builder.IncreaseIndent(), taggedStringRenderer);
                await item.AcceptAsync(nestedWriter, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                builder.Append(' ');
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
            builder.AppendLine();
            TextModuleDescriptionComponentWriter nestedWriter = new(builder.IncreaseIndent(), taggedStringRenderer);
            await propertyComponent.Value.AcceptAsync(nestedWriter, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            builder.Append(' ');
            await propertyComponent.Value.AcceptAsync(this, cancellationToken).ConfigureAwait(false);
        }
    }

    private static IndentedStringBuilder WriteAtom<T>(IndentedStringBuilder builder, ITaggedStringRenderer taggedStringRenderer, T value, ImmutableArray<string> hints) => value switch
    {
        null => builder.AppendLine("null"),
        TaggedString tagged => AppendQuotedString(builder, taggedStringRenderer.Render(tagged.WithTags(hints)), '"'),
        string text when !hints.IsEmpty => AppendQuotedString(builder, taggedStringRenderer.Render(new TaggedString(text, hints)), '"'),
        string text => AppendQuotedString(builder, text, '"'),
        char character => AppendQuotedString(builder, character.ToString(), '\''),
        bool flag => builder.AppendLine(flag ? "true" : "false"),
        DateTime dateTime => builder.AppendLine(dateTime.ToString("O", CultureInfo.InvariantCulture)),
        DateTimeOffset dateTimeOffset => builder.AppendLine(dateTimeOffset.ToString("O", CultureInfo.InvariantCulture)),
        TimeSpan timeSpan => builder.AppendLine(timeSpan.ToString("c", CultureInfo.InvariantCulture)),
        Guid guid => builder.AppendLine(guid.ToString("D")),
        Enum e => builder.AppendLine(e.ToString()),
        // cyborg types
        ModuleReference moduleReference => builder.AppendLine($"-> {moduleReference.Module.ModuleId}"),
        _ => builder.AppendLine(Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.GetType().Name)
    };

    private static IndentedStringBuilder AppendQuotedString(IndentedStringBuilder builder, string value, char quote)
    {
        builder.Append(quote);
        foreach (char character in value)
        {
            _ = character switch
            {
                '\\' => builder.Append("\\\\"),
                '"' when quote == '"' => builder.Append("\\\""),
                '\'' when quote == '\'' => builder.Append("\\'"),
                '\0' => builder.Append("\\0"),
                '\a' => builder.Append("\\a"),
                '\b' => builder.Append("\\b"),
                '\f' => builder.Append("\\f"),
                '\n' => builder.Append("\\n"),
                '\r' => builder.Append("\\r"),
                '\t' => builder.Append("\\t"),
                '\v' => builder.Append("\\v"),
                _ when char.IsControl(character) => builder.Append("\\u").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture)),
                _ => builder.Append(character)
            };
        }
        return builder.AppendLine(quote);
    }
}
