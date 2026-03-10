using System.Text;

namespace Cyborg.Core.Aot.Extensions;

internal sealed class IndentedStringBuilder(StringBuilder builder, int indentSize = 4, int indentLevel = 0)
{
    public StringBuilder Raw => builder;

    public string IndentString { get; } = new(' ', indentSize * indentLevel);

    public IndentedStringBuilder IncreaseIndent(int levels = 1) => new(builder, indentSize, indentLevel + levels);

    public IndentedStringBuilder DecreaseIndent(int levels = 1) => new(builder, indentSize, Math.Max(0, indentLevel - levels));

    public void Append(string text)
    {
        if (builder.Length == 0 || builder[^1] == '\n')
        {
            builder.Append(IndentString);
        }
        builder.Append(text);
    }

    public void AppendLine(string line) => builder.Append(IndentString).AppendLine(line);

    public void AppendBlock(string block)
    {
        ReadOnlySpan<char> blockSpan = block.AsSpan();
        int startIndex = 0;
        int lineIndex;
        while (startIndex < blockSpan.Length && (lineIndex = blockSpan[startIndex..].IndexOf('\n')) != -1)
        {
            int endIndex = startIndex + lineIndex;
            // string possibly has \r\n line endings, so trim any trailing \r from the line
            builder.Append(IndentString).AppendLine(blockSpan[startIndex..endIndex].TrimEnd('\r').ToString());
            startIndex = endIndex + 1;
        }
        if (startIndex < blockSpan.Length)
        {
            builder.Append(IndentString).AppendLine(blockSpan[startIndex..].TrimEnd('\r').ToString());
        }
    }
}