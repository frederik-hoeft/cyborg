using Cyborg.Core.Runtime.Engine.Expressions.Bound;

namespace Cyborg.Core.Runtime.Engine.Expressions.Syntax;

/// <summary>Literal text that contains no recognized expression token.</summary>
internal sealed record LiteralPartSyntax(string Text) : TextPartSyntax
{
    public override BoundTextPart Bind() => new BoundLiteral(Text);
}
