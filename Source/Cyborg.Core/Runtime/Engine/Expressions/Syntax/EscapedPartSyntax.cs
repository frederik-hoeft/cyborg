using Cyborg.Core.Runtime.Engine.Expressions.Bound;

namespace Cyborg.Core.Runtime.Engine.Expressions.Syntax;

/// <summary>Escaped operator expression, preserving structural escape depth until evaluation.</summary>
internal sealed record EscapedPartSyntax(char Operator, int EscapeDepth, string Body) : TextPartSyntax
{
    public override BoundTextPart Bind() => new BoundEscape(Operator, EscapeDepth, Body);
}
