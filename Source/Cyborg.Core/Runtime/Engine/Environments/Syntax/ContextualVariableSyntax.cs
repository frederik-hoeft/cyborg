namespace Cyborg.Core.Runtime.Engine.Environments.Syntax;

/// <summary>Reserves bare self and dotted self paths independently of which contextual properties are registered.</summary>
internal static class ContextualVariableSyntax
{
    internal static bool IsContextualName(string name) =>
        name == "@" || name.StartsWith("@.", StringComparison.Ordinal);

    internal static bool IsContextualExpression(string expression) =>
        IsContextualName(expression) || expression.StartsWith("@@.", StringComparison.Ordinal);

    internal static (string Name, bool EntryPoint) ParseReference(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        // @identifier retains ordinary entry-point lookup, while @.path is contextual
        // in the current view. @@ and @@.path use the original entry point.
        if (expression.StartsWith("@@", StringComparison.Ordinal))
        {
            return (expression[1..], true);
        }
        if (IsContextualName(expression))
        {
            return (expression, false);
        }
        return expression.StartsWith('@') ? (expression[1..], true) : (expression, false);
    }
}
