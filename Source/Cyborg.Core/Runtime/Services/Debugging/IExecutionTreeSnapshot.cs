namespace Cyborg.Core.Runtime.Services.Debugging;

/// <summary>
/// Immutable point-in-time view of the currently open logical module executions.
/// Closed invocations are not retained as execution history.
/// </summary>
public interface IExecutionTreeSnapshot
{
    /// <summary>Current open roots in structured start order.</summary>
    IReadOnlyList<IExecutionTreeNode> Roots { get; }
}
