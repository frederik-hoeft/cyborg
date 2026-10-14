using Cyborg.Core.Runtime.Engine.Expressions.Evaluated;
using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Engine.Expressions.Bound;

/// <summary>Immutable structural text; interpolations and escaped tokens remain distinct from literal text.</summary>
internal sealed record BoundText(ImmutableArray<BoundTextPart> Parts) : BoundValue
{
    public override EvaluatedValue Evaluate(IExpressionResolutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ImmutableArray<EvaluatedTextPart>.Builder parts = ImmutableArray.CreateBuilder<EvaluatedTextPart>(Parts.Length);
        foreach (BoundTextPart part in Parts)
        {
            parts.Add(part.Evaluate(context));
        }
        return new EvaluatedText(parts.MoveToImmutable());
    }
}
