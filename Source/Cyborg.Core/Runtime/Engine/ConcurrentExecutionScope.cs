using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine;

internal sealed class ConcurrentExecutionScope : IConcurrentExecutionScope
{
    private readonly ActiveTransaction _activeTransaction;
    private readonly ModuleTransaction _restoreTransaction;
    private readonly ModuleTransactionForkGroup _fork;
    private readonly ITransactionCompletionPolicy _completionPolicy;
    private readonly InvocationScopeFactory _createInvocationScopeAsync;
    private readonly CancellationTokenSource _scopeCancellation = new();
    private readonly List<ConcurrentModuleExecution> _executions = [];
    private readonly List<CancellationTokenSource> _childCancellations = [];
    private ScopeState _state = ScopeState.Open;
    private bool _closedNotified;
    private bool _resourcesDisposed;
    private bool _scopeCancellationDisposed;

    public ConcurrentExecutionScope(
        ActiveTransaction activeTransaction,
        ModuleTransaction restoreTransaction,
        ModuleTransactionForkGroup fork,
        ITransactionCompletionPolicy completionPolicy,
        InvocationScopeFactory createInvocationScopeAsync)
    {
        _activeTransaction = activeTransaction;
        _restoreTransaction = restoreTransaction;
        _fork = fork;
        _completionPolicy = completionPolicy;
        _createInvocationScopeAsync = createInvocationScopeAsync;
    }

    public async ValueTask<IConcurrentModuleExecution> StartAsync(ModuleContext moduleContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleContext);
        if (_state != ScopeState.Open)
        {
            throw new InvalidOperationException("A concurrent execution scope cannot start children after it has started to close.");
        }

        ModuleTransaction childTransaction = _fork.CreateChild();
        CancellationTokenSource childCancellation = CancellationTokenSource.CreateLinkedTokenSource(_scopeCancellation.Token, cancellationToken);
        _childCancellations.Add(childCancellation);
        ModuleInvocationScope invocationScope = await _createInvocationScopeAsync(
            childTransaction,
            moduleContext.Module.ModuleId,
            moduleContext.Module.Definition,
            childCancellation.Token);
        ConcurrentModuleExecution execution = new(invocationScope, moduleContext.Module.Definition, moduleContext, childCancellation);
        _executions.Add(execution);
        execution.Start();
        return execution;
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_state != ScopeState.Open)
        {
            throw new InvalidOperationException("The concurrent execution scope is not open.");
        }

        _state = ScopeState.Closing;
        using CancellationTokenRegistration registration = cancellationToken.Register(static state => ((ConcurrentExecutionScope)state!).CancelChildren(), this);
        IModuleExecutionResult[] results;
        try
        {
            results = await WaitForChildrenAsync();
        }
        catch
        {
            await AbandonAsync();
            throw;
        }

        if (HasNestedOwnerFork())
        {
            _state = ScopeState.Open;
            throw new InvalidOperationException("A concurrent execution scope cannot close while the owning invocation has a nested fork group open.");
        }

        try
        {
            for (int i = 0; i < _executions.Count; i++)
            {
                _executions[i].Transaction.Complete(_completionPolicy.Resolve(_executions[i].Module, results[i].Status));
            }
            _fork.Continuation.Complete();
            bool joined = _fork.TryJoin(out TransactionConflict? conflict);
            await NotifyClosedAsync(joined);
            RestoreOwner();
            _state = ScopeState.Closed;
            await DisposeResourcesAsync();
            if (!joined)
            {
                throw conflict!.ToException();
            }
        }
        catch
        {
            if (_state != ScopeState.Closed)
            {
                await AbandonAsync();
            }
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_state == ScopeState.Closed || _state == ScopeState.Closing)
            {
                return;
            }

            CancelChildren();
            try
            {
                await WaitForChildrenAsync();
            }
            catch (Exception)
            {
                // Child faults are observed by abandonment. Disposal must still release the fork.
            }

            await AbandonAsync();
        }
        finally
        {
            DisposeScopeCancellation();
        }
    }

    private async Task<IModuleExecutionResult[]> WaitForChildrenAsync()
    {
        if (_executions.Count == 0)
        {
            return [];
        }

        Task<IModuleExecutionResult>[] completions = new Task<IModuleExecutionResult>[_executions.Count];
        for (int i = 0; i < _executions.Count; i++)
        {
            completions[i] = _executions[i].Completion;
        }
        return await Task.WhenAll(completions);
    }

    private bool HasNestedOwnerFork()
    {
        if (_fork.Continuation.HasOpenFork)
        {
            return true;
        }

        for (int i = 0; i < _executions.Count; i++)
        {
            if (_executions[i].Transaction.HasOpenFork)
            {
                return true;
            }
        }

        return false;
    }

    private async Task AbandonAsync()
    {
        if (_state == ScopeState.Closed)
        {
            return;
        }

        if (_fork.Lifecycle == ModuleTransactionForkLifecycle.Active)
        {
            _fork.Discard();
        }

        await NotifyClosedAsync(joined: false);
        RestoreOwner();
        _state = ScopeState.Closed;
        await DisposeResourcesAsync();
    }

    private async ValueTask NotifyClosedAsync(bool joined)
    {
        if (_closedNotified)
        {
            return;
        }

        _closedNotified = true;
        for (int i = 0; i < _executions.Count; i++)
        {
            await _executions[i].Scope.CloseAsync(joined);
        }
    }

    private void RestoreOwner()
    {
        if (!ReferenceEquals(_activeTransaction.Current, _restoreTransaction))
        {
            _activeTransaction.Retarget(_restoreTransaction);
        }
    }

    private void CancelChildren()
    {
        try
        {
            _scopeCancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        for (int i = 0; i < _executions.Count; i++)
        {
            _executions[i].Cancel();
        }
    }

    private async ValueTask DisposeResourcesAsync()
    {
        if (_resourcesDisposed)
        {
            return;
        }

        _resourcesDisposed = true;
        for (int i = _executions.Count - 1; i >= 0; i--)
        {
            await _executions[i].Scope.DisposeAsync();
        }

        for (int i = 0; i < _childCancellations.Count; i++)
        {
            _childCancellations[i].Dispose();
        }
    }

    private void DisposeScopeCancellation()
    {
        if (_scopeCancellationDisposed)
        {
            return;
        }

        _scopeCancellationDisposed = true;
        _scopeCancellation.Dispose();
    }

    private enum ScopeState
    {
        Open,
        Closing,
        Closed,
    }
}

internal delegate ValueTask<ModuleInvocationScope> InvocationScopeFactory(
    ModuleTransaction childTransaction,
    string moduleId,
    IModule module,
    CancellationToken cancellationToken);

internal sealed class ConcurrentModuleExecution : IConcurrentModuleExecution
{
    private readonly ModuleInvocationScope _scope;
    private readonly ModuleContext _moduleContext;
    private readonly CancellationTokenSource _cancellation;
    private Task<IModuleExecutionResult>? _completion;

    public ConcurrentModuleExecution(ModuleInvocationScope scope, IModule module, ModuleContext moduleContext, CancellationTokenSource cancellation)
    {
        _scope = scope;
        Module = module;
        _moduleContext = moduleContext;
        _cancellation = cancellation;
    }

    public IModule Module { get; }

    public ModuleInvocationScope Scope => _scope;

    public ModuleTransaction Transaction => _scope.Transaction;

    public Task<IModuleExecutionResult> Completion => _completion ?? throw new InvalidOperationException("The concurrent child execution has not been started.");

    public void Start() => _completion = ExecuteAsync();

    public void Cancel()
    {
        try
        {
            _cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task<IModuleExecutionResult> ExecuteAsync()
    {
        IRuntimeEnvironment environment = _scope.Runtime.PrepareEnvironment(_moduleContext.Environment ?? ModuleEnvironment.Default);
        IModuleExecutionResult result = await _scope.Runtime.ExecuteModuleContextInCurrentScopeAsync(_moduleContext, environment, _cancellation.Token);
        await _scope.NotifyCompletedAsync(result);
        return result;
    }
}
