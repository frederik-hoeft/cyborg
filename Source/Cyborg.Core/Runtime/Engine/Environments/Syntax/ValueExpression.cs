namespace Cyborg.Core.Runtime.Engine.Environments.Syntax;

internal readonly record struct ValueExpression(ValueExpressionKind Kind, string Expression)
{
    public static ValueExpression Text { get; } = new(ValueExpressionKind.Text, string.Empty);

    public static ValueExpression Indirection(string expression) => new(ValueExpressionKind.LazyIndirection, expression);

    public static ValueExpression Capture(string expression) => new(ValueExpressionKind.EagerCapture, expression);
}
