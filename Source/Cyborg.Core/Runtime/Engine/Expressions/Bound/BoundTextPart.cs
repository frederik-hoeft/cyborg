using Cyborg.Core.Runtime.Engine.Expressions.Evaluated;
using System.Text;

namespace Cyborg.Core.Runtime.Engine.Expressions.Bound;

internal abstract record BoundTextPart
{
    public abstract EvaluatedTextPart Evaluate(IExpressionResolutionContext context);

    /// <summary>Renders a deliberately deferred textual expression, finalizing only typed escape layers.</summary>
    public abstract void AppendWithoutInterpolation(StringBuilder builder);
}
