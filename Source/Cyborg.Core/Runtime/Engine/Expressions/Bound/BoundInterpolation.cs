using Cyborg.Core.Runtime.Engine.Expressions.Evaluated;
using Cyborg.Core.Runtime.Engine.Expressions.Syntax;

namespace Cyborg.Core.Runtime.Engine.Expressions.Bound;

internal sealed record BoundInterpolation(VariableReferenceSyntax Reference) : BoundTextPart
{
    public override EvaluatedTextPart Evaluate(IExpressionResolutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TryResolve(Reference, out EvaluatedValue? value))
        {
            return new EvaluatedSplice(value);
        }
        // Missing textual references remain literal for this evaluation, without freezing the stored lazy reference.
        return new EvaluatedLiteral($"${{{Reference}}}");
    }
    public override void AppendWithoutInterpolation(System.Text.StringBuilder builder) => builder.Append("${").Append(Reference).Append('}');
}
