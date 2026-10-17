using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Core.Runtime.Engine.Expressions.Syntax;
using Cyborg.Core.Runtime.Engine.Expressions.Visitors;

namespace Cyborg.Core.Runtime.Engine.Expressions.SyntaxNodes;

/// <summary>Adapter between the combinator parse tree and immutable text syntax.</summary>
internal sealed class TextPartNode(TextPartSyntax part) : SyntaxNodeBase<ITextSyntaxVisitor>(name: null)
{
    protected override void Accept(ITextSyntaxVisitor visitor) => visitor.Visit(part);
}
