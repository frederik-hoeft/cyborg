using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Validation;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules;

public abstract class ModuleWorker<TModule>(IWorkerContext<TModule> context) : IModuleWorker where TModule : class, IModule<TModule>
{
    protected TModule Module { get; private set; } = null!;

    public string ModuleId => TModule.ModuleId;

    protected IServiceProvider ServiceProvider => context.ServiceProvider;

    protected abstract Task<bool> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken);

    async Task<bool> IModuleWorker.ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ValidationResult<TModule> result = await context.Module.ValidateAsync(runtime, ServiceProvider, cancellationToken);
        ValidationResult<TModule> overriddenResult = await ModuleValidationCallbackAsync(result, context.Module, cancellationToken);
        overriddenResult.EnsureValid();
        Module = overriddenResult.Module;
        return await ExecuteAsync(runtime, cancellationToken);
    }

    protected virtual ValueTask<ValidationResult<TModule>> ModuleValidationCallbackAsync(ValidationResult<TModule> validationResult, TModule originalModule, CancellationToken cancellationToken) => new(validationResult);

    protected IModuleArtifacts Artifacts { get; } = new DefaultModuleArtifacts();
}