using Cyborg.Core.Runtime.Engine.Expressions.Bound;
using Cyborg.Core.Runtime.Engine.Expressions.Evaluated;

namespace Cyborg.Core.Runtime.Engine.Expressions.Syntax;

/// <summary>Whole-value eager capture, resolved once at its binding boundary.</summary>
internal sealed record CaptureSyntax(VariableReferenceSyntax Reference) : ValueSyntax
{
    public override BoundValue Bind(IExpressionResolutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.TryResolve(Reference, out EvaluatedValue? value))
        {
            throw new InvalidOperationException($"Failed to capture '*{{{Reference}}}' because the target is not defined.");
        }
        return new BoundCapture(value.SnapshotForCapture());
    }
}
