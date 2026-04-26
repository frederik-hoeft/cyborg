using Cyborg.Core.Parsing.Visitors;
using System.Text;

namespace Cyborg.Core.Parsing.SyntaxNodes;

public abstract class SyntaxNodeBase : ISyntaxNode
{
    public string? Name { get; }

    public ISyntaxNode? Parent { get; set; }

    private protected SyntaxNodeBase(string? name)
    {
        Name = name;
    }

    public abstract void Accept(INodeVisitor visitor);

    public bool HasParent(string name)
    {
        bool found = false;
        for (ISyntaxNode? current = Parent; current is not null && !found; current = current.Parent)
        {
            found = current.Name == name;
        }
        return found;
    }

    public override string ToString()
    {
        StringBuilder stringBuilder = new();
        ToString(stringBuilder, 0);
        return stringBuilder.ToString();
    }

    public virtual void ToString(StringBuilder builder, int indentLevel)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Append(' ', indentLevel * 2);
        builder.Append(GetType().Name);
        builder.Append($" (Name: '{Name ?? "<unnamed>"}')");
        builder.AppendLine();
    }
}
