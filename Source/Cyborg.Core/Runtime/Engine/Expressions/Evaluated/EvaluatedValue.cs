namespace Cyborg.Core.Runtime.Engine.Expressions.Evaluated;

/// <summary>Immutable terminal structure for one evaluation. It cannot refer to live variable names.</summary>
internal abstract record EvaluatedValue
{
    internal abstract object? Materialize(ExpressionMaterializer materializer);

    internal virtual bool IsNull => false;

    internal virtual bool RequiresTextSnapshot => false;

    /// <summary>Freezes textual interpolation at a capture/transfer boundary without cloning direct CLR objects.</summary>
    internal EvaluatedValue SnapshotText() => RequiresTextSnapshot
        ? new EvaluatedTerminal(new ExpressionMaterializer().Materialize(this))
        : this;
}
