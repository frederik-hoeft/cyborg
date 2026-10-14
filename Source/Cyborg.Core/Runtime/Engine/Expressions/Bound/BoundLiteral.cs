using Cyborg.Core.Runtime.Engine.Expressions.Evaluated;

namespace Cyborg.Core.Runtime.Engine.Expressions.Bound;

internal sealed record BoundLiteral(string Text) : BoundTextPart
{
    public override EvaluatedTextPart Evaluate(IExpressionResolutionContext context) => new EvaluatedLiteral(Text);
}
