using Cyborg.Core.Parsing.SyntaxNodes;

namespace Cyborg.Core.Parsing.Parsers;

public class Optional(IParser optionalParser, string? name = null) : ParserBase(name)
{
    public override IParser NamedCopy(string name) => new Optional(optionalParser, name);

    public override bool TryParse(ReadOnlySpan<char> input, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        if (optionalParser.TryParse(input, out ISyntaxNode? inner, out int innerConsumed))
        {
            syntaxNode = new OptionalSyntaxNode(Name, inner);
            charsConsumed = innerConsumed;
            return true;
        }
        syntaxNode = Name is null ? OptionalSyntaxNode.Instance : new OptionalSyntaxNode(Name, inner: null);
        charsConsumed = 0;
        return true;
    }
}