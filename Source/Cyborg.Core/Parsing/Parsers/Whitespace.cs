using Cyborg.Core.Parsing.SyntaxNodes;

namespace Cyborg.Core.Parsing.Parsers;

public class Whitespace(string? name = null) : ParserBase(name), IParser<Whitespace>
{
    public static Whitespace Instance { get; } = new();

    public override IParser NamedCopy(string name) => new Whitespace(name);

    public override bool TryParse(ReadOnlySpan<char> input, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        for (charsConsumed = 0; charsConsumed < input.Length && char.IsWhiteSpace(input[charsConsumed]); ++charsConsumed) { }
        if (charsConsumed == 0)
        {
            syntaxNode = null;
            return false;
        }
        syntaxNode = new WhitespaceSyntaxNode(Name, charsConsumed);
        return true;
    }
}