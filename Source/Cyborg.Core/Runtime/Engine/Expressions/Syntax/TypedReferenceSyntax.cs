using Cyborg.Core.Runtime.Engine.Expressions.Bound;

namespace Cyborg.Core.Runtime.Engine.Expressions.Syntax;

/// <summary>Whole-value lazy typed reference.</summary>
internal sealed record TypedReferenceSyntax(VariableReferenceSyntax Reference) : ValueSyntax
{
    public override BoundValue Bind(IExpressionResolutionContext context) => new BoundIndirection(Reference);
}
