using Cyborg.Core.Text;
using System.Text;

namespace Cyborg.Core.Runtime.Engine.Environments.Syntax;

/// <summary>
/// Inserts escape hashes so later expression passes restore terminal text instead of evaluating it.
/// </summary>
internal static class ExpressionShield
{
    public static string Shield(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Shield(value, shieldInterpolation: true, shieldValueExpressions: true);
    }

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

    public static string FinalizeValueExpressionLiterals(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Finalize(value, finalizeInterpolation: false, finalizeValueExpressions: true);
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

    private static string Finalize(string value, bool finalizeInterpolation, bool finalizeValueExpressions)
    {
        int operatorAt = FindEscapedOperator(value, start: 0, finalizeInterpolation, finalizeValueExpressions);
        if (operatorAt < 0)
        {
            return value;
        }

        StringBuilder builder = new(value.Length);
        int index = 0;
        while (operatorAt >= 0)
        {
            builder.Append(value, index, operatorAt - index + 2);
            index = operatorAt + 3;
            operatorAt = FindEscapedOperator(value, index, finalizeInterpolation, finalizeValueExpressions);
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

    private static int FindEscapedOperator(string value, int start, bool includeInterpolation, bool includeValueExpressions)
    {
        for (int i = start; i < value.Length - 2; i++)
        {
            char operation = value[i];
            if (value[i + 1] != '{' || value[i + 2] != '#' || !ShouldProcess(operation, includeInterpolation, includeValueExpressions))
            {
                continue;
            }
            if (value.IndexOf('}', i + 3) >= 0)
            {
                return i;
            }
        }
        return -1;
    }

    private static bool ShouldProcess(char operation, bool includeInterpolation, bool includeValueExpressions) =>
        operation == '$' ? includeInterpolation : includeValueExpressions && operation is '&' or '*';
}
