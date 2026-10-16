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
        // Textual splices may reference mutable CLR objects. Freeze their stringification at capture time,
        // while preserving shallow object identity for direct typed captures.
        return new BoundCapture(value.SnapshotText());
    }
}
