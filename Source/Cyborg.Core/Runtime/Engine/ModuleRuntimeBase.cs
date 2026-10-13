using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Model;

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
        return CreateInvocationCoordinator().ExecuteAsync(new ModuleContextExecutionRequest(moduleContext, environment, cancellationToken));
    }

    public Task<IModuleExecutionResult> ExecuteAsync(ModuleConfigurationLoadResult configuration, IRuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        return CreateInvocationCoordinator().ExecuteAsync(new LoadedConfigurationExecutionRequest(configuration, environment, cancellationToken));
    }

    public Task<IModuleExecutionResult> ExecuteRootModuleAsync(ModuleConfigurationLoadResult configuration, IRuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        return CreateInvocationCoordinator().ExecuteAsync(new LoadedRootModuleExecutionRequest(configuration, environment, cancellationToken));
    }

    public Task<IModuleExecutionResult> ExecuteAsync(ModuleReference moduleReference, IRuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleReference);
        ArgumentNullException.ThrowIfNull(environment);
        return CreateInvocationCoordinator().ExecuteAsync(new ModuleReferenceExecutionRequest(moduleReference, environment, cancellationToken));
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

    public IConcurrentExecutionScope OpenConcurrentExecution() => CreateInvocationCoordinator().OpenConcurrentExecution();

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteActivatedWorkerAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(environment);
        return CreateInvocationCoordinator().ExecuteAsync(new ActivatedWorkerExecutionRequest(module, environment, cancellationToken));
    }

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteModuleContextInCurrentScopeAsync(ModuleContext moduleContext, IRuntimeEnvironment environment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moduleContext);
        ArgumentNullException.ThrowIfNull(environment);
        IRuntimeEnvironment scopedEnvironment = environmentContext.BindToCurrentTransaction(environment);
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
        IRuntimeEnvironment scopedEnvironment = environmentContext.BindToCurrentTransaction(environment);
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
        ExecuteActivatedWorkerInCurrentScopeAsync(module, environmentContext.BindToCurrentTransaction(environment), cancellationToken);

    private Task<IModuleExecutionResult> ExecuteActivatedWorkerInCurrentScopeAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken)
    {
        IServiceProvider executionServices = RequireExecutionServices();
        IRuntimeEnvironment boundEnvironment = environment.Bind(module);
        RuntimeEnvironmentContext childEnvironmentContext = environmentContext.CreateChildFrame(boundEnvironment);
        IModuleRuntime runtime = new ScopedRuntime(Root, childEnvironmentContext, runtimeServices, activeTransaction, executionServices,
            invocationContext ?? throw new InvalidOperationException("A worker runtime requires an active module invocation context."));
        return runtimeServices.Dispatcher.ExecuteAsync(module, runtime, boundEnvironment, executionServices, cancellationToken);
    }

    private ModuleInvocationCoordinator CreateInvocationCoordinator() =>
        new(Root, environmentContext, runtimeServices, activeTransaction, serviceProvider, invocationContext);

    private IServiceProvider RequireExecutionServices() =>
        serviceProvider ?? throw new InvalidOperationException("Module execution requires a service provider capable of creating execution scopes.");
}
