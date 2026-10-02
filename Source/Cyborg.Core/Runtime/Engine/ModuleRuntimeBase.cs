using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Model;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Runtime.Engine;

internal abstract class ModuleRuntimeBase
(
    RuntimeEnvironmentContext environmentContext,
    ModuleRuntimeServices runtimeServices,
    ActiveTransaction activeTransaction,
    IServiceProvider? serviceProvider = null,
    ModuleInvocationContext? invocationContext = null
) : IModuleRuntime, IModuleExecutionRuntime
{
    private readonly ITransactionCompletionPolicy _completionPolicy = runtimeServices.CompletionPolicy;

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

        await using IConcurrentExecutionScope scope = OpenConcurrentExecution();
        Task<IModuleExecutionResult>[] completions = new Task<IModuleExecutionResult>[moduleContexts.Count];
        for (int i = 0; i < moduleContexts.Count; i++)
        {
            ModuleContext moduleContext = moduleContexts[i] ?? throw new ArgumentException("Concurrent module contexts cannot contain null entries.", nameof(moduleContexts));
            IConcurrentModuleExecution execution = await scope.StartAsync(moduleContext, cancellationToken);
            completions[i] = execution.Completion;
        }
        IModuleExecutionResult[] results = await Task.WhenAll(completions);
        await scope.CloseAsync(cancellationToken);
        return results;
    }

    public IConcurrentExecutionScope OpenConcurrentExecution()
    {
        ModuleTransaction restoreTransaction = activeTransaction.Current;
        ModuleTransactionForkGroup fork = restoreTransaction.Fork();
        activeTransaction.Retarget(fork.Continuation);
        return new ConcurrentExecutionScope(activeTransaction, restoreTransaction, fork, _completionPolicy, CreateInvocationScopeAsync);
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
        ModuleConfigurationLoadResult configuration, IRuntimeEnvironment environment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        runtimeServices.ModuleRegistry.ApplySeed(activeTransaction.Current, configuration.RegistrySeed);
        return ((IModuleExecutionRuntime)this).ExecuteModuleContextInCurrentScopeAsync(configuration.ModuleContext, environment, cancellationToken);
    }

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteLoadedRootModuleInCurrentScopeAsync(
        ModuleConfigurationLoadResult configuration, IRuntimeEnvironment environment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        runtimeServices.ModuleRegistry.ApplySeed(activeTransaction.Current, configuration.RegistrySeed);
        return ((IModuleExecutionRuntime)this).ExecuteModuleReferenceInCurrentScopeAsync(configuration.ModuleContext.Module, environment, cancellationToken);
    }

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteModuleReferenceInCurrentScopeAsync(
        ModuleReference moduleReference, IRuntimeEnvironment environment, CancellationToken cancellationToken)
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
        IModuleRuntime runtime = new ScopedRuntime(Root, childEnvironmentContext, runtimeServices, activeTransaction, executionServices,
            invocationContext ?? throw new InvalidOperationException("A worker runtime requires an active module invocation context."));
        return runtimeServices.Dispatcher.ExecuteAsync(module, runtime, boundEnvironment, executionServices, cancellationToken);
    }

    private async Task<IModuleExecutionResult> ExecuteInNewScopeAsync(ModuleExecutionRequest request)
    {
        ModuleTransactionForkGroup fork = activeTransaction.Current.Fork();
        ModuleTransaction childTransaction = fork.CreateChild();
        fork.Continuation.Complete();
        ModuleInvocationScope? invocationScope = null;
        try
        {
            invocationScope = await CreateInvocationScopeAsync(childTransaction, request.ModuleId, request.Module, request.CancellationToken);
            IRuntimeEnvironment scopedEnvironment = invocationScope.BindEnvironment(request.Environment);
            IModuleExecutionResult result = await request.ExecuteInCurrentScopeAsync(invocationScope.Runtime, scopedEnvironment);
            await invocationScope.NotifyCompletedAsync(result);
            childTransaction.Complete(_completionPolicy.Resolve(request.Module, result.Status));
            bool joined = fork.TryJoin(out TransactionConflict? conflict);
            await invocationScope.CloseAsync(joined);
            if (!joined)
            {
                throw conflict!.ToException();
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

    private async ValueTask<ModuleInvocationScope> CreateInvocationScopeAsync(ModuleTransaction childTransaction, string moduleId, IModule module, CancellationToken cancellationToken)
    {
        IServiceProvider services = RequireExecutionServices();
        IServiceScopeFactory scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        AsyncServiceScope executionScope = scopeFactory.CreateAsyncScope();
        try
        {
            ActiveTransaction childActiveTransaction = new(childTransaction);
            runtimeServices.ModuleRegistry.BindExecutionScope(executionScope.ServiceProvider, childActiveTransaction);
            runtimeServices.Transactional.BindExecutionScope(executionScope.ServiceProvider, childActiveTransaction);
            RuntimeEnvironmentContext childEnvironmentContext = environmentContext.CreateTransactionView(childActiveTransaction);
            ModuleInvocationContext childInvocation = CreateInvocationContext(moduleId, module);
            ScopedRuntime scopedRuntime = new(Root, childEnvironmentContext, runtimeServices, childActiveTransaction, executionScope.ServiceProvider, childInvocation);
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

    private ModuleInvocationContext CreateInvocationContext(string moduleId, IModule module) =>
        new(ModuleExecutionId.Create(), invocationContext?.ExecutionId, moduleId, module.Name, module.Group, module);

    private IServiceProvider RequireExecutionServices() =>
        serviceProvider ?? throw new InvalidOperationException("Module execution requires a service provider capable of creating execution scopes.");
}
