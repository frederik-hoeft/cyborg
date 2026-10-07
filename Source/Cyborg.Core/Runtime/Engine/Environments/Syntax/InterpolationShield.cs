using Cyborg.Core.Text;
using System.Text;

namespace Cyborg.Core.Runtime.Engine.Environments.Syntax;

/// <summary>
/// Inserts one escape hash so a later textual pass restores a terminal snapshot instead of evaluating it.
/// </summary>
internal static class InterpolationShield
{
    public static object ShieldText(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value is string text)
        {
            return Shield(text);
        }
        if (value is TaggedString tagged)
        {
            return tagged.WithValue(Shield(tagged.Value));
        }
        return value;
    }

    public static string Shield(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        int insertAt = FindOperatorBrace(value, start: 0);
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
            insertAt = FindOperatorBrace(value, index);
        }
        builder.Append(value, index, value.Length - index);
        return builder.ToString();
    }

    private static int FindOperatorBrace(string value, int start)
    {
        for (int i = start; i < value.Length - 1; i++)
        {
            if (value[i + 1] != '{' || value[i] is not ('$' or '&' or '*'))
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
}
