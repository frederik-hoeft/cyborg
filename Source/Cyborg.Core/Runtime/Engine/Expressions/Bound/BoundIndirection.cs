using Cyborg.Core.Runtime.Engine.Expressions.Evaluated;
using Cyborg.Core.Runtime.Engine.Expressions.Syntax;

namespace Cyborg.Core.Runtime.Engine.Expressions.Bound;

/// <summary>Lazy typed reference: its resolution context is supplied at evaluation, not retained at definition.</summary>
internal sealed record BoundIndirection(VariableReferenceSyntax Reference) : BoundValue
{
    public override EvaluatedValue Evaluate(IExpressionResolutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.TryResolve(Reference, out EvaluatedValue? value))
        {
            throw new InvalidOperationException($"Failed to resolve indirection '{Reference}' because the target is not defined.");
        }
        return value;
    }
    public override object PresentStoredValue() => $"&{{{Reference}}}";
}
