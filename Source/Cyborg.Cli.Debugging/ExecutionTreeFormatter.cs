using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Debugging;
using System.Text;

namespace Cyborg.Cli.Debugging;

internal static class ExecutionTreeFormatter
{
    internal static string FormatTree(IExecutionTreeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Roots.Count == 0)
        {
            return "(no active execution)";
        }

        StringBuilder builder = new();
        foreach (IExecutionTreeNode root in snapshot.Roots)
        {
            AppendNode(builder, root, depth: 0);
        }
        return builder.ToString().TrimEnd();
    }

    internal static string FormatStack(IReadOnlyList<IExecutionTreeNode> stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        if (stack.Count == 0)
        {
            return "(no active stack)";
        }

        StringBuilder builder = new();
        for (int index = 0; index < stack.Count; index++)
        {
            builder.Append('#').Append(index).Append(' ').Append(FormatNode(stack[index])).AppendLine();
        }
        return builder.ToString().TrimEnd();
    }

    private static void AppendNode(StringBuilder builder, IExecutionTreeNode node, int depth)
    {
        builder.Append(' ', depth * 2).Append("* ").Append(FormatNode(node)).AppendLine();
        foreach (IExecutionTreeNode child in node.Children)
        {
            AppendNode(builder, child, depth + 1);
        }
    }

    private static string FormatNode(IExecutionTreeNode node) =>
        $"{ModuleIdentity.Format(node.ModuleId, node.Name, node.Group)} [{FormatState(node.State, node.ExitStatus)}]";

    private static string FormatState(ExecutionTreeNodeState state, ModuleExitStatus? exitStatus) =>
        state switch
        {
            ExecutionTreeNodeState.Running => "running",
            ExecutionTreeNodeState.Completed => $"completed: {exitStatus?.ToString() ?? "Unknown"}",
            ExecutionTreeNodeState.Paused => "paused",
            ExecutionTreeNodeState.Current => "paused/current",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown execution-tree node state.")
        };
}
