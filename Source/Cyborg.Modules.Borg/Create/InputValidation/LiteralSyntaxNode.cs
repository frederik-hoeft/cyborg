using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Core.Parsing.Visitors;

namespace Cyborg.Modules.Borg.Create.InputValidation;

internal sealed class LiteralSyntaxNode(string? name, string value) : SyntaxNodeBase<string>(name, value)
{
    // just for validation. We don't care about the value
    public override void Accept(INodeVisitor visitor) => throw new NotSupportedException();
}