using Cyborg.Core.Runtime.Engine.Expressions.Evaluated;
using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Engine.Expressions.Bound;

/// <summary>Applies source tags to textual results while preserving typed non-text CLR values.</summary>
internal sealed record BoundWithTags(BoundValue Inner, ImmutableHashSet<string> Tags, bool PreserveTaggedTextKind) : BoundValue
{
    public override EvaluatedValue Evaluate(IExpressionResolutionContext context) =>
        new EvaluatedWithTags(Inner.Evaluate(context), Tags, PreserveTaggedTextKind);
}
