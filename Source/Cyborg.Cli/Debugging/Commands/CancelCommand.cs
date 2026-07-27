using Cyborg.Core.Modules.Debugging;

namespace Cyborg.Cli.Debugging.Commands;

public sealed class CancelCommand : IDebugReplCommand
{
    public string Name => "cancel";

    public IReadOnlyList<string> Aliases { get; } = ["q", "quit"];

    public string Description => "Cancel the current module and terminate workflow execution.";

    public ValueTask<DebugReplCommandResult> ExecuteAsync(DebugReplCommandInput input, IDebugPauseContext context, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(input.Arguments))
        {
            return ValueTask.FromResult(DebugReplCommandResult.Error("Usage: cancel"));
        }

        return ValueTask.FromResult(DebugReplCommandResult.Resume(DebugResumeAction.Cancel));
    }
}
