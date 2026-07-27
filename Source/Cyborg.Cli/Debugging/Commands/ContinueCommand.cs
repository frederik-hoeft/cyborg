using Cyborg.Core.Modules.Debugging;

namespace Cyborg.Cli.Debugging.Commands;

public sealed class ContinueCommand : IDebugReplCommand
{
    public string Name => "continue";

    public IReadOnlyList<string> Aliases { get; } = ["c", "resume"];

    public string Description => "Continue workflow execution until the next breakpoint.";

    public ValueTask<DebugReplCommandResult> ExecuteAsync(DebugReplCommandInput input, IDebugPauseContext context, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(input.Arguments))
        {
            return ValueTask.FromResult(DebugReplCommandResult.Error("Usage: continue"));
        }

        return ValueTask.FromResult(DebugReplCommandResult.Resume(DebugResumeAction.Continue));
    }
}
