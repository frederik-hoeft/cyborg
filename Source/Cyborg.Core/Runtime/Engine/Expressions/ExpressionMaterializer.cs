using Cyborg.Core.Runtime.Engine.Expressions.Evaluated;
using Cyborg.Core.Text;
using System.Collections.Immutable;
using System.Text;

namespace Cyborg.Core.Runtime.Engine.Expressions;

/// <summary>Converts terminal evaluated values to consumer-facing CLR values, without interpreting their text as source syntax.</summary>
internal sealed class ExpressionMaterializer
{
    public object? Materialize(EvaluatedValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Materialize(this);
    }

    internal void AppendInterpolation(EvaluatedValue value, StringBuilder builder, ImmutableHashSet<string>.Builder tags)
    {
        object? result = Materialize(value);
        switch (result)
        {
            case TaggedString tagged:
                builder.Append(tagged.Value);
                tags.UnionWith(tagged.Tags);
                break;
            case string text:
                builder.Append(text);
                break;
            default:
                builder.Append(result);
                break;
        }
    }
}
