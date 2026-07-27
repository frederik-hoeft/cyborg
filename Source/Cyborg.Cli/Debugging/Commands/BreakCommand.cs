using Cyborg.Core.Modules.Debugging;

namespace Cyborg.Cli.Debugging.Commands;

/// <summary>
/// Handles <c>break at</c>, <c>break ls</c>, and <c>break rm</c> subcommands.
/// </summary>
public sealed class BreakCommand(IDebugReplIo io) : IDebugReplCommand
{
    public string Name => "break";

    public IReadOnlyList<string> Aliases { get; } = ["b"];

    public string Description => "Manage breakpoints: 'break at <expr>', 'break ls', 'break rm <n>'.";

    // TODO: rework entire REPL command parsing (maybe we can use ConsoleAppFramework's command parsing for this?)
    public ValueTask<DebugReplCommandResult> ExecuteAsync(DebugReplCommandInput input, IDebugPauseContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Arguments))
        {
            return ValueTask.FromResult(DebugReplCommandResult.Error("Usage: break at <expression> | break ls | break rm <number>"));
        }

        if (!TrySplitFirstToken(input.Arguments, out string subcommand, out string rest))
        {
            return ValueTask.FromResult(DebugReplCommandResult.Error("Usage: break at <expression> | break ls | break rm <number>"));
        }

        if (subcommand.Equals("at", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(rest))
            {
                return ValueTask.FromResult(DebugReplCommandResult.Error("Usage: break at <expression>"));
            }

            try
            {
                int id = context.Breakpoints.Add(rest);
                io.WriteLine($"Breakpoint {id} set: {rest}");
                return ValueTask.FromResult(DebugReplCommandResult.ContinueInRepl());
            }
            catch (ArgumentException ex)
            {
                // RegexParseException derives from ArgumentException on modern .NET.
                return ValueTask.FromResult(DebugReplCommandResult.Error($"Invalid breakpoint expression: {ex.Message}"));
            }
        }

        if (subcommand.Equals("ls", StringComparison.OrdinalIgnoreCase) || subcommand.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(rest))
            {
                return ValueTask.FromResult(DebugReplCommandResult.Error("Usage: break ls"));
            }

            IReadOnlyList<BreakpointExpression> list = context.Breakpoints.List();
            if (list.Count == 0)
            {
                io.WriteLine("No breakpoints set.");
            }
            else
            {
                foreach (BreakpointExpression breakpoint in list)
                {
                    io.WriteLine(breakpoint.ToString());
                }
            }
            return ValueTask.FromResult(DebugReplCommandResult.ContinueInRepl());
        }

        if (subcommand.Equals("rm", StringComparison.OrdinalIgnoreCase) || subcommand.Equals("remove", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(rest, out int id))
            {
                return ValueTask.FromResult(DebugReplCommandResult.Error("Usage: break rm <breakpoint number>"));
            }

            if (context.Breakpoints.Remove(id))
            {
                io.WriteLine($"Removed breakpoint {id}.");
            }
            else
            {
                io.WriteLine($"No breakpoint with number {id}.");
            }
            return ValueTask.FromResult(DebugReplCommandResult.ContinueInRepl());
        }

        return ValueTask.FromResult(DebugReplCommandResult.Error("Usage: break at <expression> | break ls | break rm <number>"));
    }

    private static bool TrySplitFirstToken(string text, out string first, out string rest)
    {
        text = text.Trim();
        int space = text.IndexOf(' ');
        if (space < 0)
        {
            first = text;
            rest = string.Empty;
            return first.Length > 0;
        }

        first = text[..space];
        rest = text[(space + 1)..].Trim();
        return true;
    }
}
