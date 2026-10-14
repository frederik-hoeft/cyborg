using Cyborg.Core.Runtime.Engine.Expressions.Evaluated;

namespace Cyborg.Core.Runtime.Engine.Expressions.Bound;

internal abstract record BoundTextPart
{
    public abstract EvaluatedTextPart Evaluate(IExpressionResolutionContext context);
}
