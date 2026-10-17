namespace Cyborg.Core.Runtime.Engine.Expressions.Evaluated;

/// <summary>Terminal CLR value. The wrapper is immutable, not necessarily the referenced object.</summary>
internal sealed record EvaluatedTerminal(object? Value) : EvaluatedValue
{
    internal override object? Materialize(ExpressionMaterializer materializer) => Value;

}
