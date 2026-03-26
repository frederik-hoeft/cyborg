using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments.Artifacts;
using Cyborg.Core.Modules.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Modules;

public abstract class ModuleWorker<TModule>(IWorkerContext<TModule> context) : IModuleWorker where TModule : ModuleBase, IModule<TModule>
{
    protected TModule Module { get; private set; } = null!;

    protected IModuleArtifactsBuilder Artifacts { get; private set; } = null!;

    public string ModuleId => TModule.ModuleId;

    protected IServiceProvider ServiceProvider => context.ServiceProvider;

    IModule IModuleWorker.Module => context.Module;

    protected abstract Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken);

    protected IModuleExecutionResult<TModule> Success<TResult>(TResult result) where TResult : class, IDecomposable => CreateResult(ModuleExitStatus.Success, result);

    protected IModuleExecutionResult<TModule> Success() => CreateResult(ModuleExitStatus.Success, null);

    protected IModuleExecutionResult<TModule> Failed<TResult>(TResult result) where TResult : class, IDecomposable => CreateResult(ModuleExitStatus.Failed, result);

    protected IModuleExecutionResult<TModule> Failed() => CreateResult(ModuleExitStatus.Failed, null);

    protected IModuleExecutionResult<TModule> Canceled<TResult>(TResult result) where TResult : class, IDecomposable => CreateResult(ModuleExitStatus.Canceled, result);

    protected IModuleExecutionResult<TModule> Canceled() => CreateResult(ModuleExitStatus.Canceled, null);

    protected IModuleExecutionResult<TModule> Skipped<TResult>(TResult result) where TResult : class, IDecomposable => CreateResult(ModuleExitStatus.Skipped, result);

    protected IModuleExecutionResult<TModule> Skipped() => CreateResult(ModuleExitStatus.Skipped, null);

    protected IModuleExecutionResult<TModule> WithStatus<TResult>(ModuleExitStatus status, TResult result) where TResult : class, IDecomposable => CreateResult(status, result);

    protected IModuleExecutionResult<TModule> WithStatus(ModuleExitStatus status) => CreateResult(status, null);

    private ModuleExecutionResult<TModule> CreateResult(ModuleExitStatus status, IDecomposable? result)
    {
        if (result is not null)
        {
            Artifacts.Expose(result);
        }
        return new ModuleExecutionResult<TModule>(Module, status, Artifacts);
    }

    async Task<IModuleExecutionResult> IModuleWorker.ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ValidationResult<TModule> result = await context.Module.ValidateAsync(runtime, ServiceProvider, cancellationToken);
        ValidationResult<TModule> overriddenResult = await ModuleValidationCallbackAsync(result, context.Module, cancellationToken);
        IModuleArtifactsFactory artifactsFactory = ServiceProvider.GetRequiredService<IModuleArtifactsFactory>();
        overriddenResult.EnsureValid();
        Module = overriddenResult.Module;
        Artifacts = artifactsFactory.CreateArtifacts(runtime, Module);
        return await ExecuteAsync(runtime, cancellationToken);
    }

    protected virtual ValueTask<ValidationResult<TModule>> ModuleValidationCallbackAsync(ValidationResult<TModule> validationResult, TModule originalModule, CancellationToken cancellationToken) => new(validationResult);
}