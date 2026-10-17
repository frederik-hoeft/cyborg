using System.Collections.Immutable;
using System.Text;

namespace Cyborg.Core.Runtime.Engine.Expressions.Evaluated;

/// <summary>Terminal reference result spliced into text without reparsing its materialized characters.</summary>
internal sealed record EvaluatedSplice(EvaluatedValue Value) : EvaluatedTextPart
{
    internal override void AppendTo(ExpressionMaterializer materializer, StringBuilder builder, ImmutableHashSet<string>.Builder tags) =>
        materializer.AppendInterpolation(Value, builder, tags);
}
