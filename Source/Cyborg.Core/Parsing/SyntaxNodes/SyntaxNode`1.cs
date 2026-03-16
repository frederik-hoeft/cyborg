using Cyborg.Core.Parsing.Visitors;

namespace Cyborg.Core.Parsing.SyntaxNodes;

public abstract class SyntaxNode<TVisitor>(string? name) : SyntaxNodeBase(name) where TVisitor : class, INodeVisitor
{
    public override void Accept(INodeVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        if (visitor is not TVisitor typedVisitor)
        {
            throw new InvalidOperationException(
                $"Visitor of type '{visitor.GetType().FullName}' is not valid for syntax node " +
                $"'{GetType().FullName}'. Expected '{typeof(TVisitor).FullName}'.");
        }

        Accept(typedVisitor);
    }

    protected abstract void Accept(TVisitor visitor);
}
