using Cyborg.Core.Runtime.Engine.Expressions.Evaluated;

namespace Cyborg.Core.Runtime.Engine.Expressions.Bound;

/// <summary>Immutable value retained in environment storage. Evaluation never modifies this expression.</summary>
internal abstract record BoundValue
{
    public abstract EvaluatedValue Evaluate(IExpressionResolutionContext context);
}
