using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Modules.Hooks;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments.Artifacts;
using Cyborg.Core.Modules.Validation;
using Cyborg.Core.Services.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules;

public abstract class ModuleWorker<TModule>(IWorkerContext<TModule> context) : IModuleWorker where TModule : ModuleBase, IModule<TModule>
{
    private readonly IServicePipeline<IModuleValidationHook> _validationHooks = context.ServiceProvider.GetRequiredService<IServicePipeline<IModuleValidationHook>>();
    private readonly IServicePipeline<IModulePreExecutionHook> _preExecutionHooks = context.ServiceProvider.GetRequiredService<IServicePipeline<IModulePreExecutionHook>>();
    private readonly IServicePipeline<IModulePostExecutionHook> _postExecutionHooks = context.ServiceProvider.GetRequiredService<IServicePipeline<IModulePostExecutionHook>>();

    private IModuleResultBuilder ResultBuilder { get; set; } = null!;

    protected TModule Module { get; private set; } = null!;

    protected IModuleArtifactsBuilder Artifacts { get; private set; } = null!;

    public string ModuleId => TModule.ModuleId;

    protected IServiceProvider ServiceProvider => context.ServiceProvider;

    protected ILogger Logger { get; } = context.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(TModule.ModuleId);

    IModule IModuleWorker.Module => context.Module;

    protected abstract Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken);

    protected IModuleExecutionResult<TModule> Success<TResult>(TResult result) where TResult : class, IDecomposable => ResultBuilder.Success(Module, result);

    protected IModuleExecutionResult<TModule> Success() => ResultBuilder.Success(Module);

    protected IModuleExecutionResult<TModule> Failed<TResult>(TResult result) where TResult : class, IDecomposable => ResultBuilder.Failed(Module, result);

    protected IModuleExecutionResult<TModule> Failed() => ResultBuilder.Failed(Module);

    protected IModuleExecutionResult<TModule> Canceled<TResult>(TResult result) where TResult : class, IDecomposable => ResultBuilder.Canceled(Module, result);

    protected IModuleExecutionResult<TModule> Canceled() => ResultBuilder.Canceled(Module);

    protected IModuleExecutionResult<TModule> Skipped<TResult>(TResult result) where TResult : class, IDecomposable => ResultBuilder.Skipped(Module, result);

    protected IModuleExecutionResult<TModule> Skipped() => ResultBuilder.Skipped(Module);

    protected IModuleExecutionResult<TModule> WithStatus<TResult>(ModuleExitStatus status, TResult result) where TResult : class, IDecomposable => ResultBuilder.WithStatus(Module, status, result);

    protected IModuleExecutionResult<TModule> WithStatus(ModuleExitStatus status) => ResultBuilder.WithStatus(Module, status);

    async Task<IModuleExecutionResult> IModuleWorker.ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        Logger.LogModuleValidationStarted(ModuleId);
        IValidationResult<TModule> moduleValidationResult = await context.Module.ValidateAsync(runtime, ServiceProvider, cancellationToken);
        IValidationResult<TModule> workerValidationResult = await OnValidationAsync(moduleValidationResult, context.Module, cancellationToken);
        // run validation hooks
        ModuleValidationContext<TModule> validationContext = new(workerValidationResult, runtime.Environment);
        foreach (IModuleValidationHook validationHook in _validationHooks)
        {
            IValidationResult<TModule> hookValidationResult = await validationHook.ExecuteAsync(validationContext, cancellationToken);
            validationContext = validationContext with
            {
                ValidationResult = hookValidationResult
            };
        }
        Module = validationContext.Module;
        IValidationResult<TModule> validationResult = validationContext.ValidationResult;
        // module instance is stable, construct state
        IModuleArtifactsFactory artifactsFactory = ServiceProvider.GetRequiredService<IModuleArtifactsFactory>();
        IModuleResultBuilderFactory resultBuilderFactory = ServiceProvider.GetRequiredService<IModuleResultBuilderFactory>();
        Artifacts = artifactsFactory.CreateArtifacts(runtime, Module);
        ResultBuilder = resultBuilderFactory.CreateResultBuilder(Artifacts);

        // run pre-execution hooks
        ModulePreExecutionContext preExecutionContext = new(ModuleId, validationResult, runtime, ResultBuilder);
        foreach (IModulePreExecutionHook preExecutionHook in _preExecutionHooks)
        {
            IModuleExecutionResult<TModule>? hookResult = await preExecutionHook.ExecuteAsync(Module, preExecutionContext, cancellationToken);
            if (hookResult is not null)
            {
                return runtime.Exit(hookResult);
            }
        }

        // enforce validation result
        if (!validationResult.IsValid)
        {
            if (Logger.IsEnabled(LogLevel.Warning))
            {
                Logger.LogModuleValidationFailed(ModuleId, string.Join("; ", validationResult.Errors.Select(static error => error.Message)));
            }
            validationResult.EnsureValid();
        }
        // validation passed, proceed to execution
        Logger.LogModuleValidationCompleted(ModuleId);
        IModuleExecutionResult result = await ExecuteAsync(runtime, cancellationToken);

        // run post-execution hooks
        ModulePostExecutionContext postExecutionContext = new(result, runtime);
        foreach (IModulePostExecutionHook postExecutionHook in _postExecutionHooks)
        {
            await postExecutionHook.ExecuteAsync(postExecutionContext, cancellationToken);
        }
        return result;
    }

    protected virtual ValueTask<IValidationResult<TModule>> OnValidationAsync(IValidationResult<TModule> validationResult, TModule originalModule, CancellationToken cancellationToken) =>
        ValueTask.FromResult(validationResult);
}
