using Cyborg.Core.Parsing.Visitors;
using Cyborg.Core.Runtime.Engine.Expressions.Syntax;

namespace Cyborg.Core.Runtime.Engine.Expressions.Visitors;

internal interface ITextSyntaxVisitor : INodeVisitor
{
    void Visit(TextPartSyntax part);
}
