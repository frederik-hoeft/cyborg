using System.Text.RegularExpressions;

namespace Cyborg.Core.Runtime.Engine.Environments.Syntax;

internal static class ValueExpressionParser
{
    public static ValueExpression Parse(VariableSyntaxBuilder syntax, string text)
    {
        ArgumentNullException.ThrowIfNull(syntax);
        ArgumentNullException.ThrowIfNull(text);

        MatchCollection active = syntax.ActiveReferenceRegex.Matches(text);
        if (active.Count == 0)
        {
            return ValueExpression.Text;
        }

        bool exact = active.Count == 1 && active[0].Index == 0 && active[0].Length == text.Length;
        if (!exact)
        {
            throw new FormatException(
                $"Value expression '{text}' is invalid. Lazy indirection '&{{...}}' and eager capture '*{{...}}' must occupy the entire value. " +
                "Escape a literal with '#' (for example '&{{#name}}' or '*{{#name}}').");
        }

        Match indirection = syntax.IndirectionRegex.Match(text);
        if (indirection.Success)
        {
            return ValueExpression.Indirection(indirection.Groups["expression"].Value);
        }

        Match capture = syntax.CaptureRegex.Match(text);
        if (capture.Success)
        {
            return ValueExpression.Capture(capture.Groups["expression"].Value);
        }

        int open = text.IndexOf('{');
        string body = open >= 0 && text.EndsWith('}') ? text[(open + 1)..^1] : string.Empty;
        if (text[0] == '*' && body.StartsWith('@'))
        {
            throw new FormatException(
                $"Eager capture '{text}' is invalid. The entry-point modifier '@' is reserved and is not supported in '*{{...}}'.");
        }

        throw new FormatException(
            text[0] == '&'
                ? $"Lazy indirection '{text}' is invalid. The reference must be an identifier, '@identifier', '@', or '@@'."
                : $"Eager capture '{text}' is invalid. The reference must be an identifier.");
    }
}
