using System.Collections.Immutable;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Hooks;

namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed class DebugExecutionTopology : IDebugExecutionTopologyController
{
    private readonly object _lock = new();
    private readonly Dictionary<ModuleExecutionId, LiveExecutionNode> _nodes = [];
    private readonly List<LiveExecutionNode> _roots = [];

    public int Priority => -short.MaxValue;

    public ValueTask OnStartedAsync(IModuleExecutionStartedContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_lock)
        {
            if (_nodes.ContainsKey(context.ExecutionId))
            {
                return ValueTask.CompletedTask;
            }

            LiveExecutionNode? parent = null;
            if (context.ParentExecutionId is { } parentExecutionId)
            {
                _nodes.TryGetValue(parentExecutionId, out parent);
            }

            LiveExecutionNode node = new(
                context.ExecutionId,
                context.ParentExecutionId,
                context.ModuleId,
                context.Name,
                context.Group,
                parent);
            _nodes.Add(node.ExecutionId, node);
            if (parent is null)
            {
                _roots.Add(node);
            }
            else
            {
                parent.Children.Add(node);
            }
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask OnCompletedAsync(IModuleExecutionCompletedContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_lock)
        {
            if (_nodes.TryGetValue(context.ExecutionId, out LiveExecutionNode? node))
            {
                node.State = ExecutionTreeNodeState.Completed;
                node.ExitStatus = context.Result.Status;
            }
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask OnClosedAsync(IModuleExecutionClosedContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_lock)
        {
            if (!_nodes.TryGetValue(context.ExecutionId, out LiveExecutionNode? node))
            {
                return ValueTask.CompletedTask;
            }

            if (node.Parent is null)
            {
                _roots.Remove(node);
            }
            else
            {
                node.Parent.Children.Remove(node);
            }
            Forget(node);
        }
        return ValueTask.CompletedTask;
    }

    public IExecutionTreeSnapshot CaptureTree()
    {
        lock (_lock)
        {
            if (_roots.Count == 0)
            {
                return ExecutionTreeSnapshot.Empty;
            }

            ImmutableArray<IExecutionTreeNode>.Builder roots = ImmutableArray.CreateBuilder<IExecutionTreeNode>(_roots.Count);
            foreach (LiveExecutionNode root in _roots)
            {
                roots.Add(Project(root));
            }
            return new ExecutionTreeSnapshot(roots.MoveToImmutable());
        }
    }

    public IReadOnlyList<IExecutionTreeNode> CaptureAncestry(ModuleExecutionId executionId)
    {
        lock (_lock)
        {
            if (!_nodes.TryGetValue(executionId, out LiveExecutionNode? node))
            {
                return [];
            }

            ImmutableArray<IExecutionTreeNode>.Builder stack = ImmutableArray.CreateBuilder<IExecutionTreeNode>();
            for (LiveExecutionNode? cursor = node; cursor is not null; cursor = cursor.Parent)
            {
                stack.Add(Project(cursor, children: []));
            }
            return stack.ToImmutable();
        }
    }

    public void EnrichPreparedModule(ModuleExecutionId executionId, IModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        lock (_lock)
        {
            if (_nodes.TryGetValue(executionId, out LiveExecutionNode? node))
            {
                node.Name = module.Name;
                node.Group = module.Group;
            }
        }
    }

    public bool MarkPaused(ModuleExecutionId executionId) =>
        TryTransition(executionId, ExecutionTreeNodeState.Running, ExecutionTreeNodeState.Paused);

    public bool MarkCurrent(ModuleExecutionId executionId) =>
        TryTransition(executionId, ExecutionTreeNodeState.Paused, ExecutionTreeNodeState.Current);

    public bool MarkRunning(ModuleExecutionId executionId)
    {
        lock (_lock)
        {
            if (!_nodes.TryGetValue(executionId, out LiveExecutionNode? node)
                || node.State is not ExecutionTreeNodeState.Paused and not ExecutionTreeNodeState.Current)
            {
                return false;
            }

            node.State = ExecutionTreeNodeState.Running;
            return true;
        }
    }

    private bool TryTransition(ModuleExecutionId executionId, ExecutionTreeNodeState expected, ExecutionTreeNodeState next)
    {
        lock (_lock)
        {
            if (!_nodes.TryGetValue(executionId, out LiveExecutionNode? node) || node.State != expected)
            {
                return false;
            }

            node.State = next;
            return true;
        }
    }

    private void Forget(LiveExecutionNode node)
    {
        _nodes.Remove(node.ExecutionId);
        foreach (LiveExecutionNode child in node.Children)
        {
            Forget(child);
        }
    }

    private static ExecutionTreeNode Project(LiveExecutionNode node)
    {
        ImmutableArray<IExecutionTreeNode>.Builder children = ImmutableArray.CreateBuilder<IExecutionTreeNode>(node.Children.Count);
        foreach (LiveExecutionNode child in node.Children)
        {
            children.Add(Project(child));
        }
        return Project(node, children.MoveToImmutable());
    }

    private static ExecutionTreeNode Project(LiveExecutionNode node, IReadOnlyList<IExecutionTreeNode> children) =>
        new(
            node.ExecutionId,
            node.ParentExecutionId,
            node.ModuleId,
            node.Name,
            node.Group,
            node.State,
            node.ExitStatus,
            children);

    private sealed class LiveExecutionNode(
        ModuleExecutionId executionId,
        ModuleExecutionId? parentExecutionId,
        string moduleId,
        string? name,
        string? group,
        LiveExecutionNode? parent)
    {
        public ModuleExecutionId ExecutionId { get; } = executionId;

        public ModuleExecutionId? ParentExecutionId { get; } = parentExecutionId;

        public string ModuleId { get; } = moduleId;

        public string? Name { get; set; } = name;

        public string? Group { get; set; } = group;

        public LiveExecutionNode? Parent { get; } = parent;

        public List<LiveExecutionNode> Children { get; } = [];

        public ExecutionTreeNodeState State { get; set; } = ExecutionTreeNodeState.Running;

        public ModuleExitStatus? ExitStatus { get; set; }
    }
}
