namespace Cyborg.Core.Modules.Runtime.Environments.Syntax;

public class NamespaceSyntaxNode(NamespaceSyntaxNode parent, string currentNamespace) : VariableSyntaxNodeBase
{
    private NamespaceSyntaxNode? Parent => parent;

    private string CurrentNamespace => currentNamespace;

    public override string Render() => Parent is null ? CurrentNamespace : $"{Parent.Render()}.{CurrentNamespace}";

    public NamespaceSyntaxNode Combine(NamespaceSyntaxNode other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new NamespaceSyntaxNode(this, other.Render());
    }

    public VariableSyntaxNode Combine(VariableSyntaxNode variable)
    {
        ArgumentNullException.ThrowIfNull(variable);
        return new VariableSyntaxNode($"{Render()}.{variable.Render()}");
    }

    public static NamespaceSyntaxNode Root => new(null!, string.Empty);
}
