namespace Cyborg.Core.Runtime.Engine.Expressions.Evaluated;

/// <summary>Immutable terminal structure for one evaluation. It cannot refer to live variable names.</summary>
internal abstract record EvaluatedValue
{
    internal abstract object? Materialize(ExpressionMaterializer materializer);

    /// <summary>
    /// Stabilizes any deferred materialization needed for eager capture. Terminal CLR objects retain their identity;
    /// expression forms that defer observable work can override this behavior.
    /// </summary>
    internal virtual EvaluatedValue SnapshotForCapture() => this;
}
