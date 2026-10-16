using System.Collections.Immutable;
using System.Text;

namespace Cyborg.Core.Runtime.Engine.Expressions.Evaluated;

/// <summary>Terminal textual result whose literal and already-evaluated splice segments cannot be interpreted again.</summary>
internal sealed record EvaluatedText(ImmutableArray<EvaluatedTextPart> Parts) : EvaluatedValue
{
    internal override bool RequiresTextSnapshot => true;

    internal override object Materialize(ExpressionMaterializer materializer)
    {
        StringBuilder builder = new();
        ImmutableHashSet<string>.Builder tags = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (EvaluatedTextPart part in Parts)
        {
            part.AppendTo(materializer, builder, tags);
        }
        return tags.Count > 0 ? (object)new Cyborg.Core.Text.TaggedString(builder.ToString(), tags) : builder.ToString();
    }
}
