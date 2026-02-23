using Cyborg.Core.Parsing.SyntaxNodes;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Parsing.Parsers;

public class Whitespace(string? name = null) : IParser<Whitespace>
{
    public static Whitespace Instance { get; } = new();

    public virtual string? Name => name;

    public virtual IParser NamedCopy(string name) => new Whitespace(name);

    public virtual bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        int i = offset;
        while (i < input.Length && char.IsWhiteSpace(input[i]))
        {
            i++;
        }
        int consumed = i - offset;
        if (consumed == 0)
        {
            charsConsumed = 0;
            syntaxNode = null;
            return false;
        }
        charsConsumed = consumed;
        syntaxNode = new WhitespaceSyntaxNode(Name, consumed);
        return true;
    }
}