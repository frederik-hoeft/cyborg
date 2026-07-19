using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Modules.Descriptors;
using Cyborg.Core.Modules.Descriptors.Builders;
using Cyborg.Core.Modules.Descriptors.Model;
using Cyborg.Core.Modules.Descriptors.Writers;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments.Artifacts;
using Cyborg.Core.Modules.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Cyborg.Core.Modules;

public abstract class ModuleWorker<TModule>(IWorkerContext<TModule> context) : IModuleWorker where TModule : ModuleBase, IModule<TModule>
{
    protected TModule Module { get; private set; } = null!;

    protected IModuleArtifactsBuilder Artifacts { get; private set; } = null!;

    public string ModuleId => TModule.ModuleId;

    protected IServiceProvider ServiceProvider => context.ServiceProvider;

    protected ILogger Logger { get; } = context.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(TModule.ModuleId);

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

    async Task<IModuleDescriptor> IModuleWorker.PrepareAsync(IModuleRuntime runtime, CancellationToken cancellationToken) => await PrepareAsync(runtime, cancellationToken);

    private async Task<TModule> PrepareAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        Logger.LogModuleValidationStarted(ModuleId);
        ValidationResult<TModule> result = await context.Module.ValidateAsync(runtime, ServiceProvider, cancellationToken);
        ValidationResult<TModule> overriddenResult = await ModuleValidationCallbackAsync(result, context.Module, cancellationToken);
        if (!overriddenResult.IsValid)
        {
            if (Logger.IsEnabled(LogLevel.Warning))
            {
                Logger.LogModuleValidationFailed(ModuleId, string.Join("; ", overriddenResult.Errors.Select(e => e.Message)));
            }
            overriddenResult.EnsureValid();
        }
        Logger.LogModuleValidationCompleted(ModuleId);
        return overriddenResult.Module;
    }

    async Task<IModuleExecutionResult> IModuleWorker.ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        IModuleArtifactsFactory artifactsFactory = ServiceProvider.GetRequiredService<IModuleArtifactsFactory>();
        Module = await PrepareAsync(runtime, cancellationToken);

        ObjectDescriptionBuilder builder = new(new DefaultDescriptionComponentFactory());
        Module.Describe(builder);
        StringBuilder sb = new();
        await builder.Build().AcceptAsync(new TextModuleDescriptionComponentWriter(new Common.Text.IndentedStringBuilder(sb)), cancellationToken);
        string description = sb.ToString();
        Console.WriteLine(description);

        Artifacts = artifactsFactory.CreateArtifacts(runtime, Module);
        return await ExecuteAsync(runtime, cancellationToken);
    }

    protected virtual ValueTask<ValidationResult<TModule>> ModuleValidationCallbackAsync(ValidationResult<TModule> validationResult, TModule originalModule, CancellationToken cancellationToken) => new(validationResult);
}
