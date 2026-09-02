using ConsoleAppFramework;
using Cyborg.Core.Runtime.Services.Debugging;

namespace Cyborg.Cli.Debugging.Commands;

internal sealed class DebugTopologyCommands(IDebugPauseContext context, IDebugReplIo io)
{
    /// <summary>Print the current logical execution tree with the active pause highlighted.</summary>
    [Command("tree")]
    public async Task TreeAsync(CancellationToken cancellationToken)
    {
        await io.WriteLineAsync(ExecutionTreeFormatter.FormatTree(context.Tree), OutputKind.Text, cancellationToken);
    }

    /// <summary>Print the logical call stack from the current pause point to the execution-tree root.</summary>
    [Command("stack")]
    public async Task StackAsync(CancellationToken cancellationToken)
    {
        await io.WriteLineAsync(ExecutionTreeFormatter.FormatStack(context.Stack), OutputKind.Text, cancellationToken);
    }
}
