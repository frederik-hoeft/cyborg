using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Model;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Runtime.Engine;

/// <summary>
/// Owns child invocation transactions, scoped services, and structured lifecycle notifications.
/// Dispatch inside an established invocation remains the responsibility of its scoped runtime.
/// </summary>
internal sealed class ModuleInvocationCoordinator(
    IModuleRuntime root,
    RuntimeEnvironmentContext environmentContext,
    ModuleRuntimeServices runtimeServices,
    ActiveTransaction activeTransaction,
    IServiceProvider? serviceProvider,
    ModuleInvocationContext? parentInvocation)
{
    public IConcurrentExecutionScope OpenConcurrentExecution()
    {
        ModuleTransaction restoreTransaction = activeTransaction.Current;
        ModuleTransactionForkGroup fork = restoreTransaction.Fork();
        activeTransaction.Retarget(fork.Continuation);
        return new ConcurrentExecutionScope(activeTransaction, restoreTransaction, fork, runtimeServices.CompletionPolicy, this);
    }

    public async Task<IModuleExecutionResult> ExecuteAsync(ModuleExecutionTarget request)
    {
        ModuleTransactionForkGroup fork = activeTransaction.Current.Fork();
        ModuleTransaction childTransaction = fork.CreateChild();
        fork.Continuation.Complete();
        ModuleInvocationScope? invocationScope = null;
        try
        {
            invocationScope = await CreateInvocationScopeAsync(childTransaction, request.ModuleId, request.Module, request.CancellationToken);
            IRuntimeEnvironment scopedEnvironment = invocationScope.BindEnvironment(request.Environment);
            IModuleExecutionResult result = await invocationScope.Runtime.DispatchInCurrentScopeAsync(request, scopedEnvironment);
            await invocationScope.NotifyCompletedAsync(result);
            childTransaction.Complete(runtimeServices.CompletionPolicy.Resolve(request.Module, result.Status));
            bool joined = fork.TryJoin(out TransactionConflict? conflict);
            await invocationScope.CloseAsync(joined);
            if (!joined)
            {
                throw conflict!.ToException();
            }
            return result;
        }
        catch
        {
            try
            {
                if (fork.Lifecycle == ModuleTransactionForkLifecycle.Active)
                {
                    fork.Discard();
                }
            }
            finally
            {
                if (invocationScope is not null)
                {
                    await invocationScope.CloseAsync(joined: false);
                }
            }
            throw;
        }
        finally
        {
            if (invocationScope is not null)
            {
                await invocationScope.DisposeAsync();
            }
        }
    }

    internal async ValueTask<ModuleInvocationScope> CreateInvocationScopeAsync(
        ModuleTransaction childTransaction, string moduleId, IModule module, CancellationToken cancellationToken)
    {
        IServiceScopeFactory scopeFactory = RequireExecutionServices().GetRequiredService<IServiceScopeFactory>();
        AsyncServiceScope executionScope = scopeFactory.CreateAsyncScope();
        try
        {
            ActiveTransaction childActiveTransaction = new(childTransaction);
            runtimeServices.ModuleRegistry.BindExecutionScope(executionScope.ServiceProvider, childActiveTransaction);
            runtimeServices.Transactional.BindExecutionScope(executionScope.ServiceProvider, childActiveTransaction);
            RuntimeEnvironmentContext childEnvironmentContext = environmentContext.RebindTransaction(childActiveTransaction);
            ModuleInvocationContext childInvocation = new(ModuleExecutionId.Create(), parentInvocation?.ExecutionId, moduleId, module.Name, module.Group, module);
            ScopedRuntime scopedRuntime = new(root, childEnvironmentContext, runtimeServices, childActiveTransaction, executionScope.ServiceProvider, childInvocation);
            ModuleInvocationScope invocationScope = new(childTransaction, executionScope, scopedRuntime, childEnvironmentContext, childInvocation);
            await invocationScope.NotifyStartedAsync(cancellationToken);
            return invocationScope;
        }
        catch
        {
            await executionScope.DisposeAsync();
            throw;
        }
    }

    private IServiceProvider RequireExecutionServices() =>
        serviceProvider ?? throw new InvalidOperationException("Module execution requires a service provider capable of creating execution scopes.");
}
