using Cyborg.Core.Parsing.Visitors;
using System.Text;

namespace Cyborg.Core.Parsing.SyntaxNodes;

public abstract class SyntaxNode<TVisitor, TResult>(string? name, TResult result) : SyntaxNode<TVisitor>(name), ISyntaxNode<TResult>
    where TVisitor : class, INodeVisitor
{
    public TResult Evaluate() => result;

    public override void ToString(StringBuilder builder, int indentLevel)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Append(' ', indentLevel * 2);
        builder.Append(GetType().Name);
        builder.Append($" (Name: '{Name ?? "<unnamed>"}', Result: '{result}')");
        builder.AppendLine();
    }
}
