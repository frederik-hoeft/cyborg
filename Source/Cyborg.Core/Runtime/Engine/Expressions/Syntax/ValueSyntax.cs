using Cyborg.Core.Runtime.Engine.Expressions.Bound;

namespace Cyborg.Core.Runtime.Engine.Expressions.Syntax;

/// <summary>Immutable parse result; binding returns a separate immutable representation at the semantic definition boundary.</summary>
internal abstract record ValueSyntax
{
    public abstract BoundValue Bind(IExpressionResolutionContext context);
}
