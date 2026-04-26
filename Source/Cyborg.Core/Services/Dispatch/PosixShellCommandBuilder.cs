using System.Text;

namespace Cyborg.Core.Services.Dispatch;

public sealed class PosixShellCommandBuilder : IPosixShellCommandBuilder
{
    public string QuoteArgument(string argument)
    {
        ValidateArgument(argument, nameof(argument));

        // Always quote. Predictable and shell-safe for POSIX parsing.
        // foo'bar -> 'foo'"'"'bar'
        return string.Concat("'", argument.Replace("'", "'\"'\"'"), "'");
    }

    public string BuildCommand(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count == 0)
        {
            throw new ArgumentException("At least one argument is required.", nameof(arguments));
        }

        StringBuilder builder = new();

        for (int i = 0; i < arguments.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(QuoteArgument(arguments[i]));
        }

        return builder.ToString();
    }

    public string BuildCommand(string executable, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        string[] allArguments = new string[arguments.Count + 1];
        allArguments[0] = executable;

        for (int i = 0; i < arguments.Count; i++)
        {
            allArguments[i + 1] = arguments[i];
        }

        return BuildCommand(allArguments);
    }

    private static void ValidateArgument(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        for (int i = 0; i < value.Length; ++i)
        {
            if (char.IsControl(value[i]))
            {
                throw new ArgumentException("Shell arguments must not contain control characters.", paramName);
            }
        }
    }
}
