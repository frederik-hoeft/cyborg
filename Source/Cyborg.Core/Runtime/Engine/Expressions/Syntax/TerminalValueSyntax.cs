using Cyborg.Core.Runtime.Engine.Expressions.Bound;

namespace Cyborg.Core.Runtime.Engine.Expressions.Syntax;

/// <summary>Arbitrary CLR object represented structurally without interpretation or deep cloning.</summary>
internal sealed record TerminalValueSyntax(object? Value) : ValueSyntax
{
    public override BoundValue Bind(IExpressionResolutionContext context) => new BoundTerminal(Value);
}
