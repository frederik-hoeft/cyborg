using Cyborg.Core.Modules.Debugging;

namespace Cyborg.Cli.Debugging.Commands;

public sealed class DetachCommand : IDebugReplCommand
{
    public string Name => "detach";

    public IReadOnlyList<string> Aliases { get; } = [];

    public string Description => "Remove all breakpoints and continue workflow execution.";

    public ValueTask<DebugReplCommandResult> ExecuteAsync(DebugReplCommandInput input, IDebugPauseContext context, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(input.Arguments))
        {
            return ValueTask.FromResult(DebugReplCommandResult.Error("Usage: detach"));
        }

        context.Detach();
        return ValueTask.FromResult(DebugReplCommandResult.Resume(DebugResumeAction.Continue));
    }
}
