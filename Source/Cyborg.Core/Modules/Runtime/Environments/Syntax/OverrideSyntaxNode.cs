namespace Cyborg.Core.Modules.Runtime.Environments.Syntax;

public class OverrideSyntaxNode(VariableSyntaxNode inner) : VariableSyntaxNodeBase
{
    public override string Render() => $"@{inner.Render()}";
}