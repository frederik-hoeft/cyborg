using System.Text;

namespace Cyborg.Cli.Debugging;

internal static class CommandLineTokenizer
{
    public static bool TryTokenize(
        string commandLine,
        out string[] arguments,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(commandLine);

        List<string> tokens = [];
        StringBuilder current = new();
        char quote = '\0';
        bool tokenStarted = false;

        for (int index = 0; index < commandLine.Length; index++)
        {
            char character = commandLine[index];

            if (quote == '\0' && char.IsWhiteSpace(character))
            {
                FlushToken(tokens, current, ref tokenStarted);
                continue;
            }

            if (character is '"' or '\'')
            {
                if (quote == '\0')
                {
                    quote = character;
                    tokenStarted = true;
                    continue;
                }

                if (quote == character)
                {
                    quote = '\0';
                    continue;
                }
            }

            if (character == '\\' && index + 1 < commandLine.Length)
            {
                char next = commandLine[index + 1];
                if (ShouldEscape(next, quote))
                {
                    current.Append(next);
                    tokenStarted = true;
                    index++;
                    continue;
                }
            }

            current.Append(character);
            tokenStarted = true;
        }

        if (quote != '\0')
        {
            arguments = [];
            error = $"Unterminated {quote} quote.";
            return false;
        }

        FlushToken(tokens, current, ref tokenStarted);
        arguments = [.. tokens];
        error = null;
        return true;
    }

    private static void FlushToken(
        List<string> tokens,
        StringBuilder current,
        ref bool tokenStarted)
    {
        if (!tokenStarted)
        {
            return;
        }

        tokens.Add(current.ToString());
        current.Clear();
        tokenStarted = false;
    }

    private static bool ShouldEscape(char character, char quote)
    {
        if (character == '\\')
        {
            return true;
        }

        if (quote == '\0')
        {
            return character is '"' or '\'' || char.IsWhiteSpace(character);
        }

        return character == quote;
    }
}
