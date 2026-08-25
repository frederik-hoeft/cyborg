using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Hooks;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Services.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Runtime;

internal sealed class ModuleExecutionDispatcher(ILogger logger)
{
    public IModuleWorker ActivateWorker(ModuleReference moduleReference, IServiceProvider? serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(moduleReference);
        IServiceProvider executionServices = serviceProvider
            ?? throw new InvalidOperationException($"Cannot activate module '{moduleReference.ModuleId}' because the runtime has no execution service provider.");
        IModuleWorkerFactory workerFactory = executionServices.GetRequiredService<IModuleWorkerFactory>();
        return workerFactory.CreateWorker(moduleReference, executionServices);
    }

    public async Task<IModuleExecutionResult> ExecuteAsync(
        IModuleWorker module,
        IModuleRuntime runtime,
        IRuntimeEnvironment environment,
        IServiceProvider? serviceProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(environment);

        logger.LogModuleDispatched(module.ModuleId, environment.Name);
        IModuleExecutionResult result;
        try
        {
            result = await module.ExecuteAsync(runtime, cancellationToken);
            if (result.Status is ModuleExitStatus.Failed or ModuleExitStatus.Canceled)
            {
                logger.LogModuleExecutionFailed(module.ModuleId, result.Status.ToString(), environment.Name);
            }
            else
            {
                logger.LogModuleCompleted(module.ModuleId, result.Status.ToString(), environment.Name);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogModuleCanceled(module.ModuleId, environment.Name);
            result = new ModuleExecutionResult(module.Module, ModuleExitStatus.Canceled, environment.CreateArtifactCollection());
        }
        catch (Exception exception)
        {
            logger.LogModuleUnhandledException(module.ModuleId, environment.Name, exception);
            result = new ModuleExecutionResult(module.Module, ModuleExitStatus.Failed, environment.CreateArtifactCollection());
        }

        await RunPostExecutionHooksAsync(module.ModuleId, result, runtime, serviceProvider);
        return result;
    }

    private async ValueTask RunPostExecutionHooksAsync(
        string moduleId,
        IModuleExecutionResult result,
        IModuleRuntime runtime,
        IServiceProvider? serviceProvider)
    {
        IServicePipeline<IModulePostExecutionHook>? postExecutionHooks;
        try
        {
            postExecutionHooks = serviceProvider?.GetService(typeof(IServicePipeline<IModulePostExecutionHook>)) as IServicePipeline<IModulePostExecutionHook>;
        }
        catch (Exception exception)
        {
            logger.LogPostExecutionHookPipelineFailed(moduleId, exception);
            return;
        }
        if (postExecutionHooks is null)
        {
            return;
        }

        ModulePostExecutionContext context = new(result, runtime);
        foreach (IModulePostExecutionHook postExecutionHook in postExecutionHooks)
        {
            try
            {
                await postExecutionHook.ExecuteAsync(context, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogPostExecutionHookFailed(moduleId, postExecutionHook.GetType().FullName ?? postExecutionHook.GetType().Name, exception);
            }
        }
    }
}
