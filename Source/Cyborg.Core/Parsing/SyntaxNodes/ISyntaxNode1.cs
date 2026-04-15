namespace Cyborg.Core.Parsing.SyntaxNodes;

public interface ISyntaxNode<out TResult> : ISyntaxNode
{
    TResult Evaluate();
}
