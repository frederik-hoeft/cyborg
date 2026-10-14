using Cyborg.Core.Runtime.Engine.Expressions.Bound;
using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Engine.Expressions.Syntax;

/// <summary>Textual value composed of literal, interpolated, and escaped segments in source order.</summary>
internal sealed record TextValueSyntax(ImmutableArray<TextPartSyntax> Parts) : ValueSyntax
{
    public override BoundValue Bind(IExpressionResolutionContext context)
    {
        ImmutableArray<BoundTextPart>.Builder parts = ImmutableArray.CreateBuilder<BoundTextPart>(Parts.Length);
        foreach (TextPartSyntax part in Parts)
        {
            parts.Add(part.Bind());
        }
        return new BoundText(parts.MoveToImmutable());
    }
}
