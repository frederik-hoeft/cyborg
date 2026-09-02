using Cyborg.Core.Runtime.Engine;

namespace Cyborg.Core.Runtime.Services.Debugging;

/// <summary>
/// Serializes frontend ownership after independent pause decisions while preserving queued pauses as
/// first-class logical debugger state.
/// </summary>
internal sealed class DebugPauseCoordinator(
    IDebugExecutionTopology topology,
    IDebugSessionState sessionState)
{
    private readonly object _lock = new();
    private readonly IDebugExecutionTopologyController _topology =
        topology as IDebugExecutionTopologyController
        ?? throw new ArgumentException("The debugger topology service must expose controller operations.", nameof(topology));
    private readonly IDebugSessionState _sessionState = sessionState ?? throw new ArgumentNullException(nameof(sessionState));
    private readonly LinkedList<DebugPauseRequest> _queue = [];
    private bool _hasActivePause;

    public async ValueTask<DebugPauseLease?> AcquireAsync(
        ModuleExecutionId? executionId,
        long sessionGeneration,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (executionId is { } pausedExecutionId)
        {
            _topology.MarkPaused(pausedExecutionId);
        }

        DebugPauseRequest request = new(executionId, sessionGeneration);
        bool acquiredImmediately;
        lock (_lock)
        {
            acquiredImmediately = !_hasActivePause;
            if (acquiredImmediately)
            {
                _hasActivePause = true;
            }
            else
            {
                request.QueueNode = _queue.AddLast(request);
            }
        }

        if (!acquiredImmediately)
        {
            using CancellationTokenRegistration registration = cancellationToken.UnsafeRegister(
                static state =>
                {
                    (DebugPauseCoordinator coordinator, DebugPauseRequest queuedRequest) =
                        ((DebugPauseCoordinator, DebugPauseRequest))state!;
                    coordinator.CancelQueued(queuedRequest);
                },
                (this, request));
            bool acquired = await request.Completion.Task.ConfigureAwait(false);
            if (!acquired)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return null;
            }
            if (cancellationToken.IsCancellationRequested)
            {
                Release(request);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        if (_sessionState.Generation != request.SessionGeneration)
        {
            Release(request);
            return null;
        }

        if (executionId is { } currentExecutionId)
        {
            _topology.MarkCurrent(currentExecutionId);
        }
        return new DebugPauseLease(this, request);
    }

    internal void Release(DebugPauseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExecutionId is { } executionId)
        {
            _topology.MarkRunning(executionId);
        }

        DebugPauseRequest? next = null;
        List<DebugPauseRequest>? stale = null;
        lock (_lock)
        {
            if (!_hasActivePause)
            {
                return;
            }

            while (_queue.First is { } node)
            {
                _queue.RemoveFirst();
                DebugPauseRequest candidate = node.Value;
                candidate.QueueNode = null;
                if (candidate.SessionGeneration != _sessionState.Generation)
                {
                    stale ??= [];
                    stale.Add(candidate);
                    continue;
                }

                next = candidate;
                break;
            }

            if (next is null)
            {
                _hasActivePause = false;
            }
        }

        if (stale is not null)
        {
            foreach (DebugPauseRequest staleRequest in stale)
            {
                if (staleRequest.ExecutionId is { } staleExecutionId)
                {
                    _topology.MarkRunning(staleExecutionId);
                }
                staleRequest.Completion.TrySetResult(false);
            }
        }

        next?.Completion.TrySetResult(true);
    }

    private void CancelQueued(DebugPauseRequest request)
    {
        bool removed = false;
        lock (_lock)
        {
            if (request.QueueNode is { } node)
            {
                _queue.Remove(node);
                request.QueueNode = null;
                removed = true;
            }
        }

        if (!removed)
        {
            return;
        }

        if (request.ExecutionId is { } executionId)
        {
            _topology.MarkRunning(executionId);
        }
        request.Completion.TrySetResult(false);
    }
}
