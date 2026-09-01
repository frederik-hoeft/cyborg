using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Model;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Runtime.Engine;

internal abstract class ModuleRuntimeBase
(
    RuntimeEnvironmentContext environmentContext,
    ModuleRuntimeServices runtimeServices,
    ModuleTransaction transaction,
    IServiceProvider? serviceProvider = null,
    ModuleInvocationContext? invocationContext = null
) : IModuleRuntime, IModuleExecutionRuntime
{
    public IRuntimeEnvironment GlobalEnvironment => environmentContext.GlobalEnvironment;

    public IRuntimeEnvironment ParentEnvironment => environmentContext.ParentEnvironment;

    public IRuntimeEnvironment Environment => environmentContext.Environment;

    protected abstract IModuleRuntime Root { get; }

    ModuleInvocationContext? IModuleExecutionRuntime.InvocationContext => invocationContext;

    IServiceProvider? IModuleExecutionRuntime.ExecutionServices => serviceProvider;

    public Task<IModuleExecutionResult> ExecuteAsync(ModuleContext moduleContext, IRuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleContext);
        ArgumentNullException.ThrowIfNull(environment);
        return ExecuteInNewScopeAsync(new ModuleContextExecutionRequest(moduleContext, environment, cancellationToken));
    }

    public Task<IModuleExecutionResult> ExecuteAsync(ModuleConfigurationLoadResult configuration, IRuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        return ExecuteInNewScopeAsync(new LoadedConfigurationExecutionRequest(configuration, environment, cancellationToken));
    }

    public Task<IModuleExecutionResult> ExecuteRootModuleAsync(ModuleConfigurationLoadResult configuration, IRuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        return ExecuteInNewScopeAsync(new LoadedRootModuleExecutionRequest(configuration, environment, cancellationToken));
    }

    public Task<IModuleExecutionResult> ExecuteAsync(ModuleReference moduleReference, IRuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleReference);
        ArgumentNullException.ThrowIfNull(environment);
        return ExecuteInNewScopeAsync(new ModuleReferenceExecutionRequest(moduleReference, environment, cancellationToken));
    }

    public async Task<IReadOnlyList<IModuleExecutionResult>> ExecuteConcurrentlyAsync(IReadOnlyList<ModuleContext> moduleContexts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleContexts);
        if (moduleContexts.Count == 0)
        {
            return [];
        }

        ModuleTransactionForkGroup fork = transaction.Fork();
        fork.Continuation.Complete();
        List<ConcurrentExecutionBranch> branches = new(moduleContexts.Count);
        try
        {
            for (int i = 0; i < moduleContexts.Count; i++)
            {
                ModuleContext moduleContext = moduleContexts[i]
                    ?? throw new ArgumentException("Concurrent module contexts cannot contain null entries.", nameof(moduleContexts));
                ModuleTransaction childTransaction = fork.CreateChild();
                ModuleInvocationScope invocationScope = await CreateInvocationScopeAsync(
                    childTransaction,
                    moduleContext.Module.ModuleId,
                    moduleContext.Module.Definition,
                    cancellationToken);
                branches.Add(new ConcurrentExecutionBranch(invocationScope, moduleContext));
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
            bool joined = fork.TryJoin(out TransactionConflict? conflict);
            await NotifyConcurrentBranchesClosedAsync(branches, joined);
            if (!joined)
            {
                throw CreateReconciliationException(conflict!);
            }
            return results;
        }
        catch
        {
            try
            {
                if (fork.Lifecycle == ModuleTransactionForkLifecycle.Active)
                {
                    fork.Discard();
                }
            }
            finally
            {
                await NotifyConcurrentBranchesClosedAsync(branches, joined: false);
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

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteActivatedWorkerAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(environment);
        return ExecuteInNewScopeAsync(new ActivatedWorkerExecutionRequest(module, environment, cancellationToken));
    }

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteModuleContextInCurrentScopeAsync(ModuleContext moduleContext, IRuntimeEnvironment environment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moduleContext);
        ArgumentNullException.ThrowIfNull(environment);
        IRuntimeEnvironment scopedEnvironment = environmentContext.BindEnvironment(environment);
        return runtimeServices.ContextRunner.ExecuteAsync(this, moduleContext, scopedEnvironment, cancellationToken);
    }

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteLoadedConfigurationInCurrentScopeAsync(
        ModuleConfigurationLoadResult configuration,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        runtimeServices.ModuleRegistry.ApplySeed(transaction, configuration.RegistrySeed);
        return ((IModuleExecutionRuntime)this).ExecuteModuleContextInCurrentScopeAsync(configuration.ModuleContext, environment, cancellationToken);
    }

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteLoadedRootModuleInCurrentScopeAsync(
        ModuleConfigurationLoadResult configuration,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        runtimeServices.ModuleRegistry.ApplySeed(transaction, configuration.RegistrySeed);
        return ((IModuleExecutionRuntime)this).ExecuteModuleReferenceInCurrentScopeAsync(configuration.ModuleContext.Module, environment, cancellationToken);
    }

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteModuleReferenceInCurrentScopeAsync(ModuleReference moduleReference, IRuntimeEnvironment environment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moduleReference);
        ArgumentNullException.ThrowIfNull(environment);
        IRuntimeEnvironment scopedEnvironment = environmentContext.BindEnvironment(environment);
        IModuleWorker worker = runtimeServices.Dispatcher.ActivateWorker(moduleReference, RequireExecutionServices());
        return ExecuteActivatedWorkerInCurrentScopeAsync(worker, scopedEnvironment, cancellationToken);
    }

    public IRuntimeEnvironment PrepareEnvironment(ModuleEnvironment moduleEnvironment, IReadOnlyCollection<string>? overrideResolutionTags = null) =>
        environmentContext.PrepareEnvironment(moduleEnvironment, overrideResolutionTags);

    public IRuntimeEnvironment? ResolveEnvironmentReference(ModuleEnvironmentReference environmentReference) =>
        environmentContext.ResolveEnvironmentReference(environmentReference);

    public IModuleExecutionResult Exit<TModule>(IModuleExecutionResult<TModule> result) where TModule : ModuleBase, IModuleDefinition =>
        runtimeServices.ArtifactPublisher.Publish(result, this, Environment);

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteActivatedWorkerInCurrentScopeAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken) =>
        ExecuteActivatedWorkerInCurrentScopeAsync(module, environmentContext.BindEnvironment(environment), cancellationToken);

    private Task<IModuleExecutionResult> ExecuteActivatedWorkerInCurrentScopeAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken)
    {
        IServiceProvider executionServices = RequireExecutionServices();
        IRuntimeEnvironment boundEnvironment = environment.Bind(module);
        RuntimeEnvironmentContext childEnvironmentContext = environmentContext.CreateChild(boundEnvironment);
        IModuleRuntime runtime = new ScopedRuntime(
            Root,
            childEnvironmentContext,
            runtimeServices,
            transaction,
            executionServices,
            invocationContext ?? throw new InvalidOperationException("A worker runtime requires an active module invocation context."));
        return runtimeServices.Dispatcher.ExecuteAsync(module, runtime, boundEnvironment, executionServices, cancellationToken);
    }

    private async Task<IModuleExecutionResult> ExecuteInNewScopeAsync(ModuleExecutionRequest request)
    {
        ModuleTransactionForkGroup fork = transaction.Fork();
        ModuleTransaction childTransaction = fork.CreateChild();
        fork.Continuation.Complete();
        ModuleInvocationScope? invocationScope = null;
        try
        {
            invocationScope = await CreateInvocationScopeAsync(
                childTransaction,
                request.ModuleId,
                request.Module,
                request.CancellationToken);
            IRuntimeEnvironment scopedEnvironment = invocationScope.BindEnvironment(request.Environment);
            IModuleExecutionResult result = await request.ExecuteInCurrentScopeAsync(invocationScope.Runtime, scopedEnvironment);
            await invocationScope.NotifyCompletedAsync(result);
            childTransaction.Complete();
            bool joined = fork.TryJoin(out TransactionConflict? conflict);
            await invocationScope.CloseAsync(joined);
            if (!joined)
            {
                throw CreateReconciliationException(conflict!);
            }
            return result;
        }
        catch
        {
            try
            {
                if (fork.Lifecycle == ModuleTransactionForkLifecycle.Active)
                {
                    fork.Discard();
                }
            }
            finally
            {
                if (invocationScope is not null)
                {
                    await invocationScope.CloseAsync(joined: false);
                }
            }
            throw;
        }
        finally
        {
            if (invocationScope is not null)
            {
                await invocationScope.DisposeAsync();
            }
        }
    }

    private async ValueTask<ModuleInvocationScope> CreateInvocationScopeAsync(
        ModuleTransaction childTransaction,
        string moduleId,
        IModule module,
        CancellationToken cancellationToken)
    {
        IServiceProvider services = RequireExecutionServices();
        IServiceScopeFactory scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        AsyncServiceScope executionScope = scopeFactory.CreateAsyncScope();
        try
        {
            runtimeServices.ModuleRegistry.BindExecutionScope(executionScope.ServiceProvider, childTransaction);
            runtimeServices.Transactional.BindExecutionScope(executionScope.ServiceProvider, childTransaction);
            RuntimeEnvironmentContext childEnvironmentContext = environmentContext.CreateTransactionView(childTransaction);
            ModuleInvocationContext childInvocation = CreateInvocationContext(moduleId, module);
            ScopedRuntime scopedRuntime = new(
                Root,
                childEnvironmentContext,
                runtimeServices,
                childTransaction,
                executionScope.ServiceProvider,
                childInvocation);
            ModuleInvocationScope invocationScope = new(childTransaction, executionScope, scopedRuntime, childEnvironmentContext, childInvocation);
            await invocationScope.NotifyStartedAsync(cancellationToken);
            return invocationScope;
        }
        catch
        {
            await executionScope.DisposeAsync();
            throw;
        }
    }

    private static async Task<IModuleExecutionResult> ExecuteConcurrentBranchAsync(ConcurrentExecutionBranch branch, CancellationToken cancellationToken)
    {
        IRuntimeEnvironment environment = branch.Scope.Runtime.PrepareEnvironment(branch.ModuleContext.Environment ?? ModuleEnvironment.Default);
        IModuleExecutionResult result = await branch.Scope.Runtime.ExecuteModuleContextInCurrentScopeAsync(branch.ModuleContext, environment, cancellationToken);
        await branch.Scope.NotifyCompletedAsync(result);
        return result;
    }

    private static async ValueTask NotifyConcurrentBranchesClosedAsync(IReadOnlyList<ConcurrentExecutionBranch> branches, bool joined)
    {
        foreach (ConcurrentExecutionBranch branch in branches)
        {
            await branch.Scope.CloseAsync(joined);
        }
    }

    private ModuleInvocationContext CreateInvocationContext(string moduleId, IModule module) =>
        new(ModuleExecutionId.Create(), invocationContext?.ExecutionId, moduleId, module.Name, module.Group, module);

    private static InvalidOperationException CreateReconciliationException(TransactionConflict conflict) =>
        new($"Module transaction reconciliation failed due to a conflict in participant '{conflict.Participant.GetType().Name}' for logical key '{conflict.LogicalKey}'.");

    private IServiceProvider RequireExecutionServices() =>
        serviceProvider ?? throw new InvalidOperationException("Module execution requires a service provider capable of creating execution scopes.");

    private sealed class ModuleInvocationScope(
        ModuleTransaction transaction,
        AsyncServiceScope serviceScope,
        IModuleExecutionRuntime runtime,
        RuntimeEnvironmentContext environmentContext,
        ModuleInvocationContext invocation) : IAsyncDisposable
    {
        private bool _closed;

        public ModuleTransaction Transaction { get; } = transaction;

        public IModuleExecutionRuntime Runtime { get; } = runtime;

        public IRuntimeEnvironment BindEnvironment(IRuntimeEnvironment environment) => environmentContext.BindEnvironment(environment);

        public ValueTask NotifyStartedAsync(CancellationToken cancellationToken) =>
            ModuleExecutionLifecycle.NotifyStartedAsync(serviceScope.ServiceProvider, invocation, Runtime, cancellationToken);

        public ValueTask NotifyCompletedAsync(IModuleExecutionResult result) =>
            ModuleExecutionLifecycle.NotifyCompletedAsync(serviceScope.ServiceProvider, invocation, Runtime, result);

        public async ValueTask CloseAsync(bool joined)
        {
            if (_closed)
            {
                return;
            }
            _closed = true;
            await ModuleExecutionLifecycle.NotifyClosedAsync(serviceScope.ServiceProvider, invocation, Runtime, joined);
        }

        public ValueTask DisposeAsync() => serviceScope.DisposeAsync();
    }

    private sealed class ConcurrentExecutionBranch(ModuleInvocationScope scope, ModuleContext moduleContext)
    {
        public ModuleInvocationScope Scope { get; } = scope;

        public ModuleTransaction Transaction => Scope.Transaction;

        public ModuleContext ModuleContext { get; } = moduleContext;
    }
}
