using Cyborg.Core.Parsing.Visitors;

namespace Cyborg.Core.Parsing.SyntaxNodes;

public sealed class EndSyntaxNode(string? name) : SyntaxNodeBase(name)
{
    public override void Accept(INodeVisitor visitor) { }
}