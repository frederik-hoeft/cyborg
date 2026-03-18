using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Core.Parsing.Visitors;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Cyborg.Core.Parsing.Parsers;

public abstract class RegexParserBase<TVisitor, TSelf>(string? name) : RegexParserBase<TSelf>(name)
    where TSelf : RegexParserBase<TVisitor, TSelf>, IRegexOwner
    where TVisitor : class, INodeVisitor
{
    protected abstract bool TryCreateSyntaxNode([NotNull] Match match, [NotNullWhen(true)] out SyntaxNode<TVisitor>? syntaxNode);

    protected sealed override bool TryCreateSyntaxNode([NotNull] Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode)
    {
        if (TryCreateSyntaxNode(match, out SyntaxNode<TVisitor>? typedNode))
        {
            syntaxNode = typedNode;
            return true;
        }
        syntaxNode = null;
        return false;
    }
}

