using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Core.Parsing.Visitors;

namespace Cyborg.Modules.Borg.Create.InputValidation;

internal sealed class NumberSyntaxNode(string? name, int value) : SyntaxNodeBase<int>(name, value)
{
    // just for validation. We don't care about the value
    public override void Accept(INodeVisitor visitor) => throw new NotSupportedException();
}