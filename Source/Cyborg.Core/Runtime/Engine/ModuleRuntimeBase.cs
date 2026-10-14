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
        return CreateInvocationCoordinator().ExecuteAsync(ModuleExecutionTarget.ForContext(moduleContext, environment, cancellationToken));
    }

    public Task<IModuleExecutionResult> ExecuteAsync(ModuleConfigurationLoadResult configuration, IRuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        return CreateInvocationCoordinator().ExecuteAsync(ModuleExecutionTarget.ForConfiguration(configuration, environment, cancellationToken));
    }

    public Task<IModuleExecutionResult> ExecuteRootModuleAsync(ModuleConfigurationLoadResult configuration, IRuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        return CreateInvocationCoordinator().ExecuteAsync(ModuleExecutionTarget.ForRootModule(configuration, environment, cancellationToken));
    }

    public Task<IModuleExecutionResult> ExecuteAsync(ModuleReference moduleReference, IRuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleReference);
        ArgumentNullException.ThrowIfNull(environment);
        return CreateInvocationCoordinator().ExecuteAsync(ModuleExecutionTarget.ForReference(moduleReference, environment, cancellationToken));
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
        return CreateInvocationCoordinator().ExecuteAsync(ModuleExecutionTarget.ForWorker(module, environment, cancellationToken));
    }

    public IRuntimeEnvironment PrepareEnvironment(ModuleEnvironment moduleEnvironment, IReadOnlyCollection<string>? overrideResolutionTags = null) =>
        environmentContext.PrepareEnvironment(moduleEnvironment, overrideResolutionTags);

    public IRuntimeEnvironment? ResolveEnvironmentReference(ModuleEnvironmentReference environmentReference) =>
        environmentContext.ResolveEnvironmentReference(environmentReference);

    public IModuleExecutionResult Exit<TModule>(IModuleExecutionResult<TModule> result) where TModule : ModuleBase, IModuleDefinition =>
        runtimeServices.ArtifactPublisher.Publish(result, this, Environment);

    internal Task<IModuleExecutionResult> DispatchInCurrentScopeAsync(ModuleExecutionTarget target, IRuntimeEnvironment environment)
    {
        ModuleTargetExecutor executor = new(this, Root, environmentContext, runtimeServices, activeTransaction, RequireExecutionServices(),
            invocationContext ?? throw new InvalidOperationException("Module dispatch requires an active invocation context."));
        return executor.ExecuteAsync(target, environment);
    }

    private ModuleInvocationCoordinator CreateInvocationCoordinator() =>
        new(Root, environmentContext, runtimeServices, activeTransaction, serviceProvider, invocationContext);

    private IServiceProvider RequireExecutionServices() =>
        serviceProvider ?? throw new InvalidOperationException("Module execution requires a service provider capable of creating execution scopes.");
}
