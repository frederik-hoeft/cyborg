using Cyborg.Core.Parsing.Visitors;
using System.Text;

namespace Cyborg.Core.Parsing.SyntaxNodes;

public abstract class ValidationSyntaxNode(string? name, string? match = null) : SyntaxNodeBase(name)
{
    public sealed override void Accept(INodeVisitor visitor) { }

    public override void ToString(StringBuilder builder, int indentLevel)
    {
        if (string.IsNullOrEmpty(Name))
        {
            base.ToString(builder, indentLevel);
            return;
        }
        ArgumentNullException.ThrowIfNull(builder);
        builder.Append(' ', indentLevel * 2);
        builder.Append(GetType().Name);
        builder.Append($" (Name: '{Name ?? "<unnamed>"}', Result: '{match}')");
        builder.AppendLine();
    }
}