namespace Cyborg.Core.Runtime.Engine.Expressions.Evaluated;

/// <summary>Immutable terminal structure for one evaluation. It cannot refer to live variable names.</summary>
internal abstract record EvaluatedValue
{
    internal abstract object? Materialize(ExpressionMaterializer materializer);
}
