using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Runtime.Engine;

internal abstract class ModuleRuntimeBase
(
    RuntimeEnvironmentContext environmentContext,
    ModuleRuntimeOperations operations,
    ExecutionTransaction transaction,
    IServiceProvider? serviceProvider = null
) : IModuleRuntime, IModuleExecutionRuntime
{
    public IRuntimeEnvironment GlobalEnvironment => environmentContext.GlobalEnvironment;

    public IRuntimeEnvironment ParentEnvironment => environmentContext.ParentEnvironment;

    public IRuntimeEnvironment Environment => environmentContext.Environment;

    protected abstract IModuleRuntime Root { get; }

    protected abstract IModuleRuntime? Parent { get; }

    public Task<IModuleExecutionResult> ExecuteAsync(ModuleContext moduleContext, IRuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleContext);
        ArgumentNullException.ThrowIfNull(environment);
        return ExecuteInNewScopeAsync(
            (runtime, scopedEnvironment) => runtime.ExecuteModuleContextInCurrentScopeAsync(moduleContext, scopedEnvironment, cancellationToken),
            environment);
    }

    public Task<IModuleExecutionResult> ExecuteAsync(ModuleReference moduleReference, IRuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleReference);
        ArgumentNullException.ThrowIfNull(environment);
        return ExecuteInNewScopeAsync(
            (runtime, scopedEnvironment) => runtime.ExecuteModuleReferenceInCurrentScopeAsync(moduleReference, scopedEnvironment, cancellationToken),
            environment);
    }

    public async Task<IReadOnlyList<IModuleExecutionResult>> ExecuteConcurrentlyAsync(IReadOnlyList<ModuleContext> moduleContexts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleContexts);
        if (moduleContexts.Count == 0)
        {
            return [];
        }

        ExecutionTransactionForkGroup fork = transaction.Fork();
        fork.Continuation.Complete();
        List<ConcurrentExecutionBranch> branches = new(moduleContexts.Count);
        try
        {
            IServiceProvider services = RequireExecutionServices();
            IServiceScopeFactory scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
            for (int i = 0; i < moduleContexts.Count; i++)
            {
                ModuleContext moduleContext = moduleContexts[i]
                    ?? throw new ArgumentException("Concurrent module contexts cannot contain null entries.", nameof(moduleContexts));
                ExecutionTransaction childTransaction = fork.CreateChild();
                AsyncServiceScope executionScope = scopeFactory.CreateAsyncScope();
                try
                {
                    operations.ModuleRegistry.BindExecutionScope(executionScope.ServiceProvider, childTransaction);
                    operations.TransactionalServices.BindExecutionScope(executionScope.ServiceProvider, childTransaction);
                    RuntimeEnvironmentContext childEnvironmentContext = environmentContext.CreateTransactionView(childTransaction);
                    ScopedRuntime scopedRuntime = new(Root, parent: this, childEnvironmentContext, operations, childTransaction, executionScope.ServiceProvider);
                    IRuntimeEnvironment environment = scopedRuntime.PrepareEnvironment(moduleContext.Environment ?? ModuleEnvironment.Default);
                    branches.Add(new ConcurrentExecutionBranch(childTransaction, executionScope, scopedRuntime, environment, moduleContext));
                }
                catch
                {
                    await executionScope.DisposeAsync();
                    throw;
                }
            }

            Task<IModuleExecutionResult>[] executions = new Task<IModuleExecutionResult>[branches.Count];
            for (int i = 0; i < branches.Count; i++)
            {
                executions[i] = ExecuteConcurrentBranchAsync(branches[i], cancellationToken);
            }
            IModuleExecutionResult[] results = await Task.WhenAll(executions);

            foreach (ConcurrentExecutionBranch branch in branches)
            {
                branch.Transaction.Complete();
            }
            if (!fork.TryJoin(out TransactionConflict? conflict))
            {
                throw CreateReconciliationException(conflict!);
            }
            return results;
        }
        catch
        {
            if (fork.Lifecycle == ExecutionTransactionForkLifecycle.Active)
            {
                fork.Discard();
            }
            throw;
        }
        finally
        {
            for (int i = branches.Count - 1; i >= 0; i--)
            {
                await branches[i].Scope.DisposeAsync();
            }
        }
    }

    void IModuleExecutionRuntime.ApplyModuleRegistrySeed(ModuleRegistrySeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        operations.ModuleRegistry.ApplySeed(transaction, seed);
    }

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteActivatedWorkerAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(environment);
        return ExecuteInNewScopeAsync(
            (runtime, scopedEnvironment) => runtime.ExecuteActivatedWorkerInCurrentScopeAsync(module, scopedEnvironment, cancellationToken),
            environment);
    }

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteModuleContextInCurrentScopeAsync(ModuleContext moduleContext, IRuntimeEnvironment environment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moduleContext);
        ArgumentNullException.ThrowIfNull(environment);
        IRuntimeEnvironment scopedEnvironment = environmentContext.BindEnvironment(environment);
        return operations.ContextExecutor.ExecuteAsync(this, moduleContext, scopedEnvironment, cancellationToken);
    }

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteModuleReferenceInCurrentScopeAsync(ModuleReference moduleReference, IRuntimeEnvironment environment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moduleReference);
        ArgumentNullException.ThrowIfNull(environment);
        IRuntimeEnvironment scopedEnvironment = environmentContext.BindEnvironment(environment);
        IModuleWorker worker = operations.ExecutionDispatcher.ActivateWorker(moduleReference, RequireExecutionServices());
        return ExecuteActivatedWorkerInCurrentScopeAsync(worker, scopedEnvironment, cancellationToken);
    }

    public IRuntimeEnvironment PrepareEnvironment(ModuleEnvironment moduleEnvironment, IReadOnlyCollection<string>? overrideResolutionTags = null) =>
        environmentContext.PrepareEnvironment(moduleEnvironment, overrideResolutionTags);

    public IRuntimeEnvironment? ResolveEnvironmentReference(ModuleEnvironmentReference environmentReference) =>
        environmentContext.ResolveEnvironmentReference(environmentReference);

    public IModuleExecutionResult Exit<TModule>(IModuleExecutionResult<TModule> result) where TModule : ModuleBase, IModuleDefinition =>
        operations.ArtifactPublisher.Publish(result, this, Environment);

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteActivatedWorkerInCurrentScopeAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken) =>
        ExecuteActivatedWorkerInCurrentScopeAsync(module, environmentContext.BindEnvironment(environment), cancellationToken);

    private Task<IModuleExecutionResult> ExecuteActivatedWorkerInCurrentScopeAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken)
    {
        IServiceProvider executionServices = RequireExecutionServices();
        IRuntimeEnvironment boundEnvironment = environment.Bind(module);
        RuntimeEnvironmentContext childEnvironmentContext = environmentContext.CreateChild(boundEnvironment);
        IModuleRuntime runtime = new ScopedRuntime(
            Root,
            parent: this,
            childEnvironmentContext,
            operations,
            transaction,
            executionServices);
        return operations.ExecutionDispatcher.ExecuteAsync(module, runtime, boundEnvironment, executionServices, cancellationToken);
    }

    private async Task<IModuleExecutionResult> ExecuteInNewScopeAsync(Func<IModuleExecutionRuntime, IRuntimeEnvironment, Task<IModuleExecutionResult>> executeAsync, IRuntimeEnvironment environment)
    {
        ExecutionTransactionForkGroup fork = transaction.Fork();
        ExecutionTransaction childTransaction = fork.CreateChild();
        fork.Continuation.Complete();
        try
        {
            IServiceProvider services = RequireExecutionServices();
            IServiceScopeFactory scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
            await using AsyncServiceScope executionScope = scopeFactory.CreateAsyncScope();
            operations.ModuleRegistry.BindExecutionScope(executionScope.ServiceProvider, childTransaction);
            operations.TransactionalServices.BindExecutionScope(executionScope.ServiceProvider, childTransaction);
            RuntimeEnvironmentContext childEnvironmentContext = environmentContext.CreateTransactionView(childTransaction);
            IModuleExecutionRuntime scopedRuntime = new ScopedRuntime(
                Root,
                parent: this,
                childEnvironmentContext,
                operations,
                childTransaction,
                executionScope.ServiceProvider);
            IRuntimeEnvironment scopedEnvironment = childEnvironmentContext.BindEnvironment(environment);
            IModuleExecutionResult result = await executeAsync(scopedRuntime, scopedEnvironment);
            childTransaction.Complete();
            if (!fork.TryJoin(out TransactionConflict? conflict))
            {
                throw CreateReconciliationException(conflict!);
            }
            return result;
        }
        catch
        {
            if (fork.Lifecycle == ExecutionTransactionForkLifecycle.Active)
            {
                fork.Discard();
            }
            throw;
        }
    }

    private static async Task<IModuleExecutionResult> ExecuteConcurrentBranchAsync(ConcurrentExecutionBranch branch, CancellationToken cancellationToken) =>
        await branch.Runtime.ExecuteModuleContextInCurrentScopeAsync(branch.ModuleContext, branch.Environment, cancellationToken);

    private static InvalidOperationException CreateReconciliationException(TransactionConflict conflict) =>
        new($"Module transaction reconciliation failed due to a conflict in participant '{conflict.Participant.GetType().Name}' for logical key '{conflict.LogicalKey}'.");

    private IServiceProvider RequireExecutionServices() =>
        serviceProvider ?? throw new InvalidOperationException("Module execution requires a service provider capable of creating execution scopes.");

    private sealed class ConcurrentExecutionBranch(ExecutionTransaction transaction, AsyncServiceScope scope, IModuleExecutionRuntime runtime, IRuntimeEnvironment environment, ModuleContext moduleContext)
    {
        public ExecutionTransaction Transaction { get; } = transaction;

        public AsyncServiceScope Scope { get; } = scope;

        public IModuleExecutionRuntime Runtime { get; } = runtime;

        public IRuntimeEnvironment Environment { get; } = environment;

        public ModuleContext ModuleContext { get; } = moduleContext;
    }
}
