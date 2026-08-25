using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments;
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

    public IRuntimeEnvironment GlobalEnvironment => _environmentContext.GlobalEnvironment;

    public IRuntimeEnvironment ParentEnvironment => _environmentContext.ParentEnvironment;

    public IRuntimeEnvironment Environment => _environmentContext.Environment;

    private protected abstract IModuleRuntime Root { get; }

    private protected abstract IModuleRuntime? Parent { get; }

    private protected ModuleRuntimeBase(RuntimeEnvironmentContext environmentContext, ILoggerFactory loggerFactory, IServiceProvider? serviceProvider = null)
    {
        ArgumentNullException.ThrowIfNull(environmentContext);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _environmentContext = environmentContext;
        _loggerFactory = loggerFactory;
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
            runtime => runtime.ExecuteModuleContextInCurrentScopeAsync(moduleContext, environment, cancellationToken));
    }

    public Task<IModuleExecutionResult> ExecuteAsync(
        ModuleReference moduleReference,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleReference);
        ArgumentNullException.ThrowIfNull(environment);
        return ExecuteInNewScopeAsync(
            runtime => runtime.ExecuteModuleReferenceInCurrentScopeAsync(moduleReference, environment, cancellationToken));
    }

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteActivatedWorkerAsync(
        IModuleWorker module,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(environment);
        return ExecuteInNewScopeAsync(
            runtime => runtime.ExecuteActivatedWorkerInCurrentScopeAsync(module, environment, cancellationToken));
    }

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteModuleContextInCurrentScopeAsync(
        ModuleContext moduleContext,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moduleContext);
        ArgumentNullException.ThrowIfNull(environment);
        return _contextExecutor.ExecuteAsync(this, moduleContext, environment, cancellationToken);
    }

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteModuleReferenceInCurrentScopeAsync(
        ModuleReference moduleReference,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moduleReference);
        ArgumentNullException.ThrowIfNull(environment);
        IModuleWorker worker = _executionDispatcher.ActivateWorker(moduleReference, RequireExecutionServices());
        return ExecuteActivatedWorkerInCurrentScopeAsync(worker, environment, cancellationToken);
    }

    public IRuntimeEnvironment PrepareEnvironment(ModuleEnvironment moduleEnvironment, IReadOnlyCollection<string>? overrideResolutionTags = null) =>
        _environmentContext.PrepareEnvironment(moduleEnvironment, overrideResolutionTags);

    public IRuntimeEnvironment? ResolveEnvironmentReference(ModuleEnvironmentReference environmentReference) =>
        _environmentContext.ResolveEnvironmentReference(environmentReference);

    public IModuleExecutionResult Exit<TModule>(IModuleExecutionResult<TModule> result) where TModule : ModuleBase, IModuleDefinition
    {
        IModuleRuntime responsibleRuntime = Parent ?? this;
        return _artifactPublisher.Publish(result, responsibleRuntime, Environment);
    }

    Task<IModuleExecutionResult> IModuleExecutionRuntime.ExecuteActivatedWorkerInCurrentScopeAsync(
        IModuleWorker module,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken) =>
        ExecuteActivatedWorkerInCurrentScopeAsync(module, environment, cancellationToken);

    private Task<IModuleExecutionResult> ExecuteActivatedWorkerInCurrentScopeAsync(
        IModuleWorker module,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken)
    {
        IServiceProvider executionServices = RequireExecutionServices();
        IRuntimeEnvironment boundEnvironment = environment.Bind(module);
        RuntimeEnvironmentContext childEnvironmentContext = _environmentContext.CreateChild(boundEnvironment);
        IModuleRuntime runtime = new ScopedRuntime(Root, parent: this, childEnvironmentContext, _loggerFactory, executionServices);
        return _executionDispatcher.ExecuteAsync(module, runtime, boundEnvironment, executionServices, cancellationToken);
    }

    private async Task<IModuleExecutionResult> ExecuteInNewScopeAsync(Func<IModuleExecutionRuntime, Task<IModuleExecutionResult>> executeAsync)
    {
        IServiceProvider services = RequireExecutionServices();
        IServiceScopeFactory scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        await using AsyncServiceScope executionScope = scopeFactory.CreateAsyncScope();
        IModuleExecutionRuntime scopedRuntime = new ScopedRuntime(Root, parent: this, _environmentContext, _loggerFactory, executionScope.ServiceProvider);
        return await executeAsync(scopedRuntime);
    }

    private IServiceProvider RequireExecutionServices() =>
        _serviceProvider ?? throw new InvalidOperationException("Module execution requires a service provider capable of creating execution scopes.");
}
