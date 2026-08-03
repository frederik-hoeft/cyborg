using System.Text;

namespace Cyborg.Core.Common.Text;

public sealed class IndentedStringBuilder(
    StringBuilder builder,
    int indentLevel = 0,
    int indentSize = 2)
{
    public StringBuilder GetInnerBuilder() => builder;

    public int IndentSize => indentSize;

    public int IndentLevel => indentLevel;

    public string IndentString { get; } = new(' ', indentSize * indentLevel);

    public IndentedStringBuilder IncreaseIndent(int levels = 1)
        => new(builder, indentLevel + levels, indentSize);

    public IndentedStringBuilder DecreaseIndent(int levels = 1)
        => new(builder, Math.Max(0, indentLevel - levels), indentSize);

    public IndentedStringBuilder Append(string text)
    {
        if (builder.Length == 0 || builder[^1] == '\n')
        {
            builder.Append(IndentString);
        }

        builder.Append(text);
        return this;
    }

    public IndentedStringBuilder AppendLine(string line)
    {
        Append(line).GetInnerBuilder().AppendLine();
        return this;
    }

    public IndentedStringBuilder AppendLine()
    {
        builder.AppendLine();
        return this;
    }
}
