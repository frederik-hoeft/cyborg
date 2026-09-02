using Cyborg.Core.Runtime.Hooks;
using Cyborg.Core.Services.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Runtime.Engine;

/// <summary>Dispatches non-authoritative structured execution lifecycle observations.</summary>
internal static class ModuleExecutionLifecycle
{
    private const string STARTED_EVENT = "started";
    private const string COMPLETED_EVENT = "completed";
    private const string CLOSED_EVENT = "closed";

    public static ValueTask NotifyStartedAsync(
        IServiceProvider serviceProvider,
        ModuleInvocationContext invocation,
        IModuleRuntime runtime,
        CancellationToken cancellationToken) =>
        ForEachHookAsync(
            serviceProvider,
            invocation,
            STARTED_EVENT,
            static (hook, context, token) => hook.OnStartedAsync(context, token),
            new ModuleExecutionStartedContext(invocation, runtime),
            cancellationToken);

    public static ValueTask NotifyCompletedAsync(
        IServiceProvider serviceProvider,
        ModuleInvocationContext invocation,
        IModuleRuntime runtime,
        IModuleExecutionResult result) =>
        ForEachHookAsync(
            serviceProvider,
            invocation,
            COMPLETED_EVENT,
            static (hook, context, token) => hook.OnCompletedAsync(context, token),
            new ModuleExecutionCompletedContext(invocation, runtime, result),
            CancellationToken.None);

    public static ValueTask NotifyClosedAsync(
        IServiceProvider serviceProvider,
        ModuleInvocationContext invocation,
        IModuleRuntime runtime,
        bool joined) =>
        ForEachHookAsync(
            serviceProvider,
            invocation,
            CLOSED_EVENT,
            static (hook, context, token) => hook.OnClosedAsync(context, token),
            new ModuleExecutionClosedContext(invocation, runtime, joined),
            CancellationToken.None);

    private static async ValueTask ForEachHookAsync<TContext>(
        IServiceProvider serviceProvider,
        ModuleInvocationContext invocation,
        string eventName,
        Func<IModuleExecutionLifecycleHook, TContext, CancellationToken, ValueTask> executeAsync,
        TContext context,
        CancellationToken cancellationToken)
    {
        IServicePipeline<IModuleExecutionLifecycleHook>? hooks = ResolveHooks(serviceProvider, invocation, eventName, out ILogger? logger);
        if (hooks is null)
        {
            return;
        }

        foreach (IModuleExecutionLifecycleHook hook in hooks)
        {
            try
            {
                await executeAsync(hook, context, cancellationToken);
            }
            catch (Exception exception)
            {
                logger?.LogExecutionLifecycleHookFailed(
                    invocation.ModuleId,
                    invocation.ExecutionId.ToString(),
                    eventName,
                    hook.GetType().FullName ?? hook.GetType().Name,
                    exception);
            }
        }
    }

    private static IServicePipeline<IModuleExecutionLifecycleHook>? ResolveHooks(
        IServiceProvider serviceProvider,
        ModuleInvocationContext invocation,
        string eventName,
        out ILogger? logger)
    {
        logger = null;
        try
        {
            logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger("cyborg.core.runtime");
            return serviceProvider.GetService(typeof(IServicePipeline<IModuleExecutionLifecycleHook>)) as IServicePipeline<IModuleExecutionLifecycleHook>;
        }
        catch (Exception exception)
        {
            logger?.LogExecutionLifecycleHookPipelineFailed(invocation.ModuleId, invocation.ExecutionId.ToString(), eventName, exception);
            return null;
        }
    }
}
