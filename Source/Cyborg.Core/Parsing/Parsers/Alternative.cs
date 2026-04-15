using Cyborg.Core.Parsing.SyntaxNodes;
using System.Collections.Immutable;

namespace Cyborg.Core.Parsing.Parsers;

public class Alternative(ImmutableArray<IParser> parsers, string? name = null) : ParserBase(name)
{
    public override IParser NamedCopy(string name) => new Alternative(parsers, name);

    public override bool TryParse(ReadOnlySpan<char> input, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        foreach (IParser parser in parsers)
        {
            if (parser.TryParse(input, out ISyntaxNode? node, out int consumed))
            {
                charsConsumed = consumed;
                syntaxNode = Name is null ? node : new AlternativeSyntaxNode(Name, node);
                return true;
            }
        }
        charsConsumed = 0;
        syntaxNode = null;
        return false;
    }
}