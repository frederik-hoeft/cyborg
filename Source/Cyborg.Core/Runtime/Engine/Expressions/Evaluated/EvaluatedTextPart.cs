using System.Collections.Immutable;
using System.Text;

namespace Cyborg.Core.Runtime.Engine.Expressions.Evaluated;

internal abstract record EvaluatedTextPart
{
    internal abstract void AppendTo(ExpressionMaterializer materializer, StringBuilder builder, ImmutableHashSet<string>.Builder tags);
}
