using Cyborg.Core.Text;
using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Engine.Expressions.Evaluated;

/// <summary>Immutable source-tag wrapper, independent of the evaluated payload's CLR text kind.</summary>
internal sealed record EvaluatedWithTags(EvaluatedValue Inner, ImmutableHashSet<string> Tags, bool PreserveTaggedTextKind) : EvaluatedValue
{
    internal override object? Materialize(ExpressionMaterializer materializer)
    {
        object? value = materializer.Materialize(Inner);
        return value switch
        {
            TaggedString tagged when Tags.Count > 0 => tagged.WithTags(Tags),
            string text when Tags.Count > 0 || PreserveTaggedTextKind => new TaggedString(text, Tags),
            _ => value
        };
    }
}
