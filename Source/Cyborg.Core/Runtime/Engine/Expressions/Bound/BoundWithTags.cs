using Cyborg.Core.Runtime.Engine.Expressions.Evaluated;
using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Engine.Expressions.Bound;

/// <summary>Applies source tags to textual results while preserving typed non-text CLR values.</summary>
internal sealed record BoundWithTags(BoundValue Inner, ImmutableHashSet<string> Tags, bool PreserveTaggedTextKind) : BoundValue
{
    public override EvaluatedValue Evaluate(IExpressionResolutionContext context) =>
        new EvaluatedWithTags(Inner.Evaluate(context), Tags, PreserveTaggedTextKind);
    public override bool IsTextual => Inner.IsTextual;

    public override object? PresentStoredValue()
    {
        object? raw = Inner.PresentStoredValue();
        return raw switch
        {
            Cyborg.Core.Text.TaggedString tagged => tagged.WithTags(Tags),
            string text => new Cyborg.Core.Text.TaggedString(text, Tags),
            _ => raw
        };
    }
}
