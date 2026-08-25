using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Runtime;

public abstract class ModuleRuntimeBase : IModuleRuntime
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

    protected abstract IModuleRuntime? Parent { get; }

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
        return _contextExecutor.ExecuteAsync(this, moduleContext, environment, cancellationToken);
    }

    public Task<IModuleExecutionResult> ExecuteAsync(
        ModuleReference moduleReference,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleReference);
        ArgumentNullException.ThrowIfNull(environment);
        IModuleWorker worker = _executionDispatcher.ActivateWorker(moduleReference, _serviceProvider);
        return ExecuteWorkerAsync(worker, environment, cancellationToken);
    }

    internal Task<IModuleExecutionResult> ExecuteActivatedWorkerAsync(
        IModuleWorker module,
        EnvironmentScope scope = EnvironmentScope.Global,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(module);
        ModuleEnvironment moduleEnvironment = new()
        {
            Scope = scope,
            Name = name
        };
        IRuntimeEnvironment environment = PrepareEnvironment(moduleEnvironment);
        return ExecuteWorkerAsync(module, environment, cancellationToken);
    }

    internal Task<IModuleExecutionResult> ExecuteActivatedWorkerAsync(
        IModuleWorker module,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(environment);
        return ExecuteWorkerAsync(module, environment, cancellationToken);
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

    protected Task<IModuleExecutionResult> ExecuteModuleAsync(
        IModuleRuntime root,
        IModuleWorker module,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(environment);

        IRuntimeEnvironment boundEnvironment = environment.Bind(module);
        RuntimeEnvironmentContext childEnvironmentContext = _environmentContext.CreateChild(boundEnvironment);
        IModuleRuntime runtime = new ScopedRuntime(root, parent: this, childEnvironmentContext, _loggerFactory, _serviceProvider);
        return _executionDispatcher.ExecuteAsync(module, runtime, boundEnvironment, _serviceProvider, cancellationToken);
    }

    protected abstract Task<IModuleExecutionResult> ExecuteWorkerAsync(
        IModuleWorker module,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken);
}
