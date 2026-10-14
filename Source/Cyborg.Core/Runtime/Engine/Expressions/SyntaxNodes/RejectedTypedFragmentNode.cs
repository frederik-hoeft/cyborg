using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Core.Runtime.Engine.Expressions.Visitors;

namespace Cyborg.Core.Runtime.Engine.Expressions.SyntaxNodes;

/// <summary>A parse-tree-only marker for active typed syntax encountered outside a whole-value expression.</summary>
internal sealed class RejectedTypedFragmentNode(string source) : SyntaxNodeBase<ITextSyntaxVisitor>(name: null)
{
    protected override void Accept(ITextSyntaxVisitor visitor) =>
        throw new FormatException($"Typed expression '{source}' must occupy the entire value or be escaped with '#'.");
}
