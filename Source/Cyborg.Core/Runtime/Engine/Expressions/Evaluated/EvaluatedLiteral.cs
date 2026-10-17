using System.Collections.Immutable;
using System.Text;

namespace Cyborg.Core.Runtime.Engine.Expressions.Evaluated;

/// <summary>Terminal characters, including any newly unescaped operator syntax.</summary>
internal sealed record EvaluatedLiteral(string Text) : EvaluatedTextPart
{
    internal override void AppendTo(ExpressionMaterializer materializer, StringBuilder builder, ImmutableHashSet<string>.Builder tags) => builder.Append(Text);
}
