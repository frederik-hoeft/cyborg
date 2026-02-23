using Cyborg.Core.Parsing.SyntaxNodes;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Parsing.Parsers;

public class Alternative(ImmutableArray<IParser> parsers, string? name = null) : IParser
{
    public string? Name => name;

    public IParser NamedCopy(string name) => new Alternative(parsers, name);

    public bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        foreach (IParser parser in parsers)
        {
            if (parser.TryParse(input, offset, out ISyntaxNode? node, out int consumed))
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