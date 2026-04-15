using Cyborg.Core.Parsing.SyntaxNodes;
using System.Collections.Immutable;

namespace Cyborg.Core.Parsing.Parsers;

public class Sequence(ImmutableArray<IParser> parsers, string? name = null) : ParserBase(name)
{
    public override IParser NamedCopy(string name) => new Sequence(parsers, name);

    public override bool TryParse(ReadOnlySpan<char> input, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        charsConsumed = 0;
        ISyntaxNode? tail = null;
        foreach (IParser parser in parsers)
        {
            if (!parser.TryParse(input, out ISyntaxNode? node, out int consumed))
            {
                charsConsumed = 0;
                syntaxNode = null;
                return false;
            }
            input = input[consumed..];
            charsConsumed += consumed;
            if (tail is null)
            {
                tail = node;
            }
            else
            {
                tail = new SequentialSyntaxNode(tail, node, Name);
            }
        }
        if (tail is null)
        {
            charsConsumed = 0;
            syntaxNode = null;
            return false;
        }
        syntaxNode = tail;
        return true;
    }
}
