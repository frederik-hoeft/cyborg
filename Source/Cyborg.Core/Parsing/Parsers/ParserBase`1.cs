using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Core.Parsing.Visitors;

namespace Cyborg.Core.Parsing.Parsers;

public abstract class ParserBase<TVisitor>(string? name) : ParserBase(name) where TVisitor : class, INodeVisitor
{
    public sealed override bool TryParse(ReadOnlySpan<char> input, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        if (TryParse(input, out SyntaxNode<TVisitor>? typedNode, out charsConsumed))
        {
            syntaxNode = typedNode;
            return true;
        }
        syntaxNode = null;
        return false;
    }

    protected abstract bool TryParse(ReadOnlySpan<char> input, [NotNullWhen(true)] out SyntaxNode<TVisitor>? syntaxNode, out int charsConsumed);
}

