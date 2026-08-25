using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Transactions.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Runtime;

public abstract class ModuleRuntimeBase : IModuleRuntime, IModuleExecutionRuntime
{
    private readonly ModuleArtifactPublisher _artifactPublisher;
    private readonly ModuleContextExecutor _contextExecutor;
    private readonly ModuleExecutionDispatcher _executionDispatcher;
    private readonly RuntimeEnvironmentContext _environmentContext;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider? _serviceProvider;
    private readonly ExecutionTransaction _transaction;

    public IRuntimeEnvironment GlobalEnvironment => _environmentContext.GlobalEnvironment;

    public IRuntimeEnvironment ParentEnvironment => _environmentContext.ParentEnvironment;

    public IRuntimeEnvironment Environment => _environmentContext.Environment;

    private protected abstract IModuleRuntime Root { get; }

    private protected abstract IModuleRuntime? Parent { get; }

    private protected ModuleRuntimeBase(
        RuntimeEnvironmentContext environmentContext,
        ILoggerFactory loggerFactory,
        ExecutionTransaction transaction,
        IServiceProvider? serviceProvider = null)
    {
        ArgumentNullException.ThrowIfNull(environmentContext);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(transaction);
        _environmentContext = environmentContext;
        _loggerFactory = loggerFactory;
        _transaction = transaction;
        _serviceProvider = serviceProvider;
        ILogger logger = loggerFactory.CreateLogger("cyborg.core.runtime");
        _artifactPublisher = new ModuleArtifactPublisher(logger);
        _contextExecutor = new ModuleContextExecutor(environmentContext.SyntaxFactory, logger);
        _executionDispatcher = new ModuleExecutionDispatcher(logger);
    }

    public Task<IModuleExecutionResult> ExecuteAsync(
        ModuleContext moduleContext,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleContext);
        ArgumentNullException.ThrowIfNull(environment);
        return ExecuteInNewScopeAsync(
            (runtime, scopedEnvironment) => runtime.ExecuteModuleContextInCurrentScopeAsync(moduleContext, scopedEnvironment, cancellationToken),
            environment);
    }

    public Task<IModuleExecutionResult> ExecuteAsync(
        ModuleReference moduleReference,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleReference);
        ArgumentNullException.ThrowIfNull(environment);
        return ExecuteInNewScopeAsync(
            (runtime, scopedEnvironment) => runtime.ExecuteModuleReferenceInCurrentScopeAsync(moduleReference, scopedEnvironment, cancellationToken),
            environment);
    }

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteActivatedWorkerAsync(
        IModuleWorker module,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(environment);
        return ExecuteInNewScopeAsync(
            (runtime, scopedEnvironment) => runtime.ExecuteActivatedWorkerInCurrentScopeAsync(module, scopedEnvironment, cancellationToken),
            environment);
    }

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteModuleContextInCurrentScopeAsync(
        ModuleContext moduleContext,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moduleContext);
        ArgumentNullException.ThrowIfNull(environment);
        IRuntimeEnvironment scopedEnvironment = _environmentContext.BindEnvironment(environment);
        return _contextExecutor.ExecuteAsync(this, moduleContext, scopedEnvironment, cancellationToken);
    }

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteModuleReferenceInCurrentScopeAsync(
        ModuleReference moduleReference,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moduleReference);
        ArgumentNullException.ThrowIfNull(environment);
        IRuntimeEnvironment scopedEnvironment = _environmentContext.BindEnvironment(environment);
        IModuleWorker worker = _executionDispatcher.ActivateWorker(moduleReference, RequireExecutionServices());
        return ExecuteActivatedWorkerInCurrentScopeAsync(worker, scopedEnvironment, cancellationToken);
    }

    public IRuntimeEnvironment PrepareEnvironment(ModuleEnvironment moduleEnvironment, IReadOnlyCollection<string>? overrideResolutionTags = null) =>
        _environmentContext.PrepareEnvironment(moduleEnvironment, overrideResolutionTags);

    public IRuntimeEnvironment? ResolveEnvironmentReference(ModuleEnvironmentReference environmentReference) =>
        _environmentContext.ResolveEnvironmentReference(environmentReference);

    public IModuleExecutionResult Exit<TModule>(IModuleExecutionResult<TModule> result) where TModule : ModuleBase, IModuleDefinition =>
        _artifactPublisher.Publish(result, this, Environment);

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteActivatedWorkerInCurrentScopeAsync(
        IModuleWorker module,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken) =>
        ExecuteActivatedWorkerInCurrentScopeAsync(module, _environmentContext.BindEnvironment(environment), cancellationToken);

    private Task<IModuleExecutionResult> ExecuteActivatedWorkerInCurrentScopeAsync(
        IModuleWorker module,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken)
    {
        IServiceProvider executionServices = RequireExecutionServices();
        IRuntimeEnvironment boundEnvironment = environment.Bind(module);
        RuntimeEnvironmentContext childEnvironmentContext = _environmentContext.CreateChild(boundEnvironment);
        IModuleRuntime runtime = new ScopedRuntime(
            Root,
            parent: this,
            childEnvironmentContext,
            _loggerFactory,
            _transaction,
            executionServices);
        return _executionDispatcher.ExecuteAsync(module, runtime, boundEnvironment, executionServices, cancellationToken);
    }

    private async Task<IModuleExecutionResult> ExecuteInNewScopeAsync(
        Func<IModuleExecutionRuntime, IRuntimeEnvironment, Task<IModuleExecutionResult>> executeAsync,
        IRuntimeEnvironment environment)
    {
        ExecutionTransactionForkGroup fork = _transaction.Fork();
        ExecutionTransaction childTransaction = fork.CreateChild();
        fork.Continuation.Complete();
        try
        {
            IServiceProvider services = RequireExecutionServices();
            IServiceScopeFactory scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
            await using AsyncServiceScope executionScope = scopeFactory.CreateAsyncScope();
            RuntimeEnvironmentContext childEnvironmentContext = _environmentContext.CreateTransactionView(childTransaction);
            IModuleExecutionRuntime scopedRuntime = new ScopedRuntime(
                Root,
                parent: this,
                childEnvironmentContext,
                _loggerFactory,
                childTransaction,
                executionScope.ServiceProvider);
            IRuntimeEnvironment scopedEnvironment = childEnvironmentContext.BindEnvironment(environment);
            IModuleExecutionResult result = await executeAsync(scopedRuntime, scopedEnvironment);
            childTransaction.Complete();
            if (!fork.TryJoin(out TransactionConflict? conflict))
            {
                throw new InvalidOperationException(
                    $"Module transaction reconciliation failed due to a conflict in participant '{conflict!.Participant.GetType().Name}' for logical key '{conflict.LogicalKey}'.");
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

    private IServiceProvider RequireExecutionServices() =>
        _serviceProvider ?? throw new InvalidOperationException("Module execution requires a service provider capable of creating execution scopes.");
}
