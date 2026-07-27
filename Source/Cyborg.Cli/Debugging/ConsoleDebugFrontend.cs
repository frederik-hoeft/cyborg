using Cyborg.Core.Modules.Debugging;

namespace Cyborg.Cli.Debugging;

/// <summary>
/// Console-based debug frontend that runs a minimal extensible REPL at each breakpoint.
/// Command handlers are resolved via DI so additional commands can be registered without
/// modifying this type.
/// </summary>
internal sealed class ConsoleDebugFrontend(IDebugReplIo io, IEnumerable<IDebugReplCommand> commands) : IDebugFrontend
{
    private readonly Dictionary<string, IDebugReplCommand> _commandsByName = BuildCommandMap(commands);
    private readonly IReadOnlyList<IDebugReplCommand> _uniqueCommands = commands
        .GroupBy(static c => c.Name, StringComparer.OrdinalIgnoreCase)
        .Select(static g => g.First())
        .OrderBy(static c => c.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public async ValueTask<DebugResumeAction> PauseAsync(IDebugPauseContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        io.WriteLine(string.Empty);
        io.WriteLine($"Breakpoint hit: {context.ModuleIdentity}");
        io.WriteLine("Type 'help' for available commands.");

        while (!cancellationToken.IsCancellationRequested)
        {
            io.Write("(cyborg-dbg) ");
            string? line = io.ReadLine();
            if (line is null)
            {
                // EOF: detach and continue so unattended pipes do not hang forever.
                context.Detach();
                return DebugResumeAction.Continue;
            }

            line = line.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (!TryParse(line, out string commandName, out string arguments))
            {
                io.WriteLine("Unrecognized input. Type 'help' for available commands.");
                continue;
            }

            if (commandName.Equals("help", StringComparison.OrdinalIgnoreCase)
                || commandName.Equals("?", StringComparison.OrdinalIgnoreCase)
                || commandName.Equals("h", StringComparison.OrdinalIgnoreCase))
            {
                WriteHelp();
                continue;
            }

            if (!_commandsByName.TryGetValue(commandName, out IDebugReplCommand? command))
            {
                io.WriteLine($"Unknown command '{commandName}'. Type 'help' for available commands.");
                continue;
            }

            DebugReplCommandInput input = new(line, commandName, arguments);
            DebugReplCommandResult result = await command.ExecuteAsync(input, context, cancellationToken).ConfigureAwait(false);
            if (result.ErrorMessage is not null)
            {
                io.WriteLine(result.ErrorMessage);
            }

            if (!result.StayInRepl && result.ResumeAction is { } action)
            {
                return action;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return DebugResumeAction.Cancel;
    }

    private void WriteHelp()
    {
        io.WriteLine("Debugger commands:");
        foreach (IDebugReplCommand command in _uniqueCommands)
        {
            string aliases = command.Aliases.Count > 0
                ? $" (aliases: {string.Join(", ", command.Aliases)})"
                : string.Empty;
            io.WriteLine($"  {command.Name,-12}{aliases}");
            io.WriteLine($"               {command.Description}");
        }
        io.WriteLine("  help        (aliases: h, ?)");
        io.WriteLine("               List available debugger commands.");
    }

    private static Dictionary<string, IDebugReplCommand> BuildCommandMap(IEnumerable<IDebugReplCommand> commands)
    {
        Dictionary<string, IDebugReplCommand> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (IDebugReplCommand command in commands)
        {
            map[command.Name] = command;
            foreach (string alias in command.Aliases)
            {
                map[alias] = command;
            }
        }
        return map;
    }

    private static bool TryParse(string line, out string commandName, out string arguments)
    {
        // TODO: doesn't handle quoted arguments with spaces or tabs
        int space = line.IndexOf(' ');
        if (space < 0)
        {
            commandName = line;
            arguments = string.Empty;
            return commandName.Length > 0;
        }

        commandName = line[..space];
        arguments = line[(space + 1)..].Trim();
        return commandName.Length > 0;
    }
}
