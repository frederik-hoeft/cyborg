using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Core.Parsing.Visitors;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Parsing.Parsers;

public abstract class ParserBase<TVisitor>(string? name) : ParserBase(name) where TVisitor : class, INodeVisitor
{
    public sealed override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        if (TryParse(input, offset, out ISyntaxNode<TVisitor>? typedNode, out charsConsumed))
        {
            syntaxNode = typedNode;
            return true;
        }
        syntaxNode = null;
        return false;
    }

    protected abstract bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode<TVisitor>? syntaxNode, out int charsConsumed);
}

