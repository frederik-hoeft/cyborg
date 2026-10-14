using Cyborg.Core.Runtime.Engine.Expressions.Evaluated;

namespace Cyborg.Core.Runtime.Engine.Expressions.Bound;

/// <summary>Eagerly captured terminal structure. The captured snapshot never re-enters expression evaluation.</summary>
internal sealed record BoundCapture(EvaluatedValue Snapshot) : BoundValue
{
    public override EvaluatedValue Evaluate(IExpressionResolutionContext context) => Snapshot;
}
