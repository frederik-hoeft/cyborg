using Cyborg.Core.Modules.Debugging;

namespace Cyborg.Cli.Debugging.Commands;

public sealed class StepCommand : IDebugReplCommand
{
    public string Name => "step";

    public IReadOnlyList<string> Aliases { get; } = ["s"];

    public string Description => "Execute the next module and break again (one-shot '.*' breakpoint).";

    public ValueTask<DebugReplCommandResult> ExecuteAsync(DebugReplCommandInput input, IDebugPauseContext context, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(input.Arguments))
        {
            return ValueTask.FromResult(DebugReplCommandResult.Error("Usage: step"));
        }

        context.RequestStep();
        return ValueTask.FromResult(DebugReplCommandResult.Resume(DebugResumeAction.Continue));
    }
}
