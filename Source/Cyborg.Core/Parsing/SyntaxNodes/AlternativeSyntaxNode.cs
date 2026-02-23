using Cyborg.Core.Parsing.Visitors;
using System.Text;

namespace Cyborg.Core.Parsing.SyntaxNodes;

public class AlternativeSyntaxNode : SyntaxNodeBase
{
    public AlternativeSyntaxNode(string? name, ISyntaxNode inner) : base(name)
    {
        Inner = inner;
        Inner.Parent = this;
    }

    public ISyntaxNode Inner { get; }

    public override void Accept(INodeVisitor visitor) => Inner.Accept(visitor);

    public override void ToString(StringBuilder builder, int indentLevel)
    {
        base.ToString(builder, indentLevel);
        Inner.ToString(builder, indentLevel + 1);
    }
}
