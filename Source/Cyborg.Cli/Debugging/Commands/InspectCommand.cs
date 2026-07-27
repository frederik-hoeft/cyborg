using Cyborg.Core.Modules.Debugging;

namespace Cyborg.Cli.Debugging.Commands;

public sealed class InspectCommand(IDebugReplIo io) : IDebugReplCommand
{
    public string Name => "inspect";

    public IReadOnlyList<string> Aliases { get; } = ["i"];

    public string Description => "Print the full validated state of the current module.";

    public ValueTask<DebugReplCommandResult> ExecuteAsync(DebugReplCommandInput input, IDebugPauseContext context, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(input.Arguments))
        {
            return ValueTask.FromResult(DebugReplCommandResult.Error("Usage: inspect"));
        }

        io.WriteLine(context.Inspect());
        return ValueTask.FromResult(DebugReplCommandResult.ContinueInRepl());
    }
}
