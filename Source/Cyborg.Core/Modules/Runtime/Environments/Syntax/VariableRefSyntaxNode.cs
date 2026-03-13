namespace Cyborg.Core.Modules.Runtime.Environments.Syntax;

public sealed class VariableRefSyntaxNode(VariableSyntaxNodeBase inner) : VariableSyntaxNodeBase
{
    public override string Render() => $"${{{inner.Render()}}}";
}