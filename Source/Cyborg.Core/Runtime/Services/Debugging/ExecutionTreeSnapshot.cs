namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed class ExecutionTreeSnapshot(IReadOnlyList<IExecutionTreeNode> roots) : IExecutionTreeSnapshot
{
    public static IExecutionTreeSnapshot Empty { get; } = new ExecutionTreeSnapshot([]);

    public IReadOnlyList<IExecutionTreeNode> Roots { get; } = roots;
}
