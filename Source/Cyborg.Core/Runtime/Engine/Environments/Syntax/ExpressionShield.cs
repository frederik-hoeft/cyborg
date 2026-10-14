using Cyborg.Core.Text;
using System.Text;

namespace Cyborg.Core.Runtime.Engine.Environments.Syntax;

/// <summary>
/// Retains compatibility with legacy raw-string override and typed-value preparation APIs.
/// Normal environment resolution and generated preparation preserve structural terminals instead.
/// </summary>
internal static class ExpressionShield
{
    public static object ShieldText(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value is string text)
        {
            return Shield(text, shieldInterpolation: true, shieldValueExpressions: true);
        }
        if (value is TaggedString tagged)
        {
            return tagged.WithValue(Shield(tagged.Value, shieldInterpolation: true, shieldValueExpressions: true));
        }
        return value;
    }

    public static object ShieldValueExpressions(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value is string text)
        {
            return Shield(text, shieldInterpolation: false, shieldValueExpressions: true);
        }
        if (value is TaggedString tagged)
        {
            return tagged.WithValue(Shield(tagged.Value, shieldInterpolation: false, shieldValueExpressions: true));
        }
        return value;
    }

    private static string Shield(string value, bool shieldInterpolation, bool shieldValueExpressions)
    {
        int insertAt = FindOperatorBrace(value, start: 0, shieldInterpolation, shieldValueExpressions);
        if (insertAt < 0)
        {
            return value;
        }

        StringBuilder builder = new(value.Length + 4);
        int index = 0;
        while (insertAt >= 0)
        {
            builder.Append(value, index, insertAt - index);
            builder.Append(value[insertAt]);
            builder.Append('{');
            builder.Append('#');
            index = insertAt + 2;
            insertAt = FindOperatorBrace(value, index, shieldInterpolation, shieldValueExpressions);
        }
        builder.Append(value, index, value.Length - index);
        return builder.ToString();
    }

    private static int FindOperatorBrace(string value, int start, bool includeInterpolation, bool includeValueExpressions)
    {
        for (int i = start; i < value.Length - 1; i++)
        {
            char operation = value[i];
            if (value[i + 1] != '{' || !ShouldProcess(operation, includeInterpolation, includeValueExpressions))
            {
                continue;
            }
            if (value.IndexOf('}', i + 2) >= 0)
            {
                return i;
            }
        }
        return -1;
    }

    private static bool ShouldProcess(char operation, bool includeInterpolation, bool includeValueExpressions) =>
        operation == '$' ? includeInterpolation : includeValueExpressions && operation is '&' or '*';
}
