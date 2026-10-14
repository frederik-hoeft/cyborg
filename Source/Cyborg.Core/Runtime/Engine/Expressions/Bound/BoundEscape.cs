using Cyborg.Core.Runtime.Engine.Expressions.Evaluated;

namespace Cyborg.Core.Runtime.Engine.Expressions.Bound;

/// <summary>Escape depth is structural. One layer is removed without scanning or interpreting the newly exposed text.</summary>
internal sealed record BoundEscape(char Operator, int Depth, string Body) : BoundTextPart
{
    public override EvaluatedTextPart Evaluate(IExpressionResolutionContext context) =>
        new EvaluatedLiteral($"{Operator}{{{new string('#', Depth - 1)}{Body}}}");
    public override void AppendWithoutInterpolation(System.Text.StringBuilder builder)
    {
        builder.Append(Operator).Append('{');
        builder.Append('#', Depth - (Operator is '&' or '*' ? 1 : 0));
        builder.Append(Body).Append('}');
    }
}
