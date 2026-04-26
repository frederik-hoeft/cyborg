using System.Text;

namespace Cyborg.Core.Modules.Runtime.Environments.Syntax;

internal static class VariableSyntaxHelpers
{
    public static ReadOnlySpan<char> Join(string left, ReadOnlySpan<char> right)
    {
        if (string.IsNullOrEmpty(left))
        {
            return right;
        }

        if (right.Length == 0)
        {
            return left;
        }

        return $"{left}.{right}";
    }

    public static void Join(StringBuilder left, ReadOnlySpan<char> right)
    {
        if (left.Length == 0)
        {
            left.Append(right);
            return;
        }
        if (right.Length == 0)
        {
            return;
        }
        left.Append('.').Append(right);
    }

    public static ReadOnlySpan<char> NormalizePath(ReadOnlySpan<char> path, string paramName)
    {
        ReadOnlySpan<char> trimmed = path.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Path cannot be empty or whitespace.", paramName);
        }
        if (trimmed.StartsWith('@'))
        {
            throw new ArgumentException("Paths must not start with '@'. Use Override() instead.", paramName);
        }
        if (trimmed.StartsWith("${", StringComparison.Ordinal) || trimmed.Contains('}') || trimmed.Contains('{'))
        {
            throw new ArgumentException("Paths must not contain interpolation syntax. Use Ref() instead.", paramName);
        }
        return trimmed;
    }

    public static string NormalizeMemberName(string memberName, string paramName)
    {
        ArgumentException.ThrowIfNullOrEmpty(memberName, paramName);

        string trimmed = memberName.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Member name cannot be empty or whitespace.", paramName);
        }

        return trimmed;
    }

    public static void ThrowIfEmpty(string value, string message)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException(message);
        }
    }
}
