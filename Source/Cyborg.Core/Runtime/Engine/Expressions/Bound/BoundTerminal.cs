using Cyborg.Core.Runtime.Engine.Expressions.Evaluated;

namespace Cyborg.Core.Runtime.Engine.Expressions.Bound;

/// <summary>Immutable structural wrapper for a terminal CLR value; the CLR object itself is not deep-cloned.</summary>
internal sealed record BoundTerminal(object? Value) : BoundValue
{
    public override EvaluatedValue Evaluate(IExpressionResolutionContext context) => new EvaluatedTerminal(Value);
}
