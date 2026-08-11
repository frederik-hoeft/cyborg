using System.Text;

namespace Cyborg.Core.Common.Text;

internal sealed class IndentedStringBuilder(StringBuilder builder, int indentLevel = 0, int indentSize = 4)
{
    public StringBuilder GetInnerBuilder() => builder;

    public int IndentSize => indentSize;

    public int IndentLevel => indentLevel;

    public string IndentString { get; } = new(' ', indentSize * indentLevel);

    public IndentedStringBuilder IncreaseIndent(int levels = 1) => new(builder, indentLevel + levels, indentSize);

    public IndentedStringBuilder DecreaseIndent(int levels = 1) => new(builder, Math.Max(0, indentLevel - levels), indentSize);

    public IndentedStringBuilder Append(char c)
    {
        TryIndent();
        builder.Append(c);
        return this;
    }

    public IndentedStringBuilder Append(string text)
    {
        TryIndent();
        builder.Append(text);
        return this;
    }

    private void TryIndent()
    {
        if (builder.Length == 0 || builder[^1] == '\n')
        {
            builder.Append(IndentString);
        }
    }

    public IndentedStringBuilder AppendLine(char c) => Append(c).AppendLine();

    public IndentedStringBuilder AppendLine(string line) => Append(line).AppendLine();

    public IndentedStringBuilder AppendLine()
    {
        builder.AppendLine();
        return this;
    }
}
