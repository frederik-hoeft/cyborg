namespace Cyborg.Core.Modules.Runtime.Environments.Syntax;

public class VariableSyntaxNode(string variableName) : VariableSyntaxNodeBase
{
    public override string Render() => variableName;
}
