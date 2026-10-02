using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Runtime.Engine;

internal sealed class ModuleInvocationScope(
    ModuleTransaction transaction,
    AsyncServiceScope serviceScope,
    IModuleExecutionRuntime runtime,
    RuntimeEnvironmentContext environmentContext,
    ModuleInvocationContext invocation) : IAsyncDisposable
{
    private bool _closed;

    public ModuleTransaction Transaction { get; } = transaction;

    public IModuleExecutionRuntime Runtime { get; } = runtime;

    public IRuntimeEnvironment BindEnvironment(IRuntimeEnvironment environment) => environmentContext.BindEnvironment(environment);

    public ValueTask NotifyStartedAsync(CancellationToken cancellationToken) =>
        ModuleExecutionLifecycle.NotifyStartedAsync(serviceScope.ServiceProvider, invocation, Runtime, cancellationToken);

    public ValueTask NotifyCompletedAsync(IModuleExecutionResult result) =>
        ModuleExecutionLifecycle.NotifyCompletedAsync(serviceScope.ServiceProvider, invocation, Runtime, result);

    public async ValueTask CloseAsync(bool joined)
    {
        if (_closed)
        {
            return;
        }
        _closed = true;
        await ModuleExecutionLifecycle.NotifyClosedAsync(serviceScope.ServiceProvider, invocation, Runtime, joined);
    }

    public ValueTask DisposeAsync() => serviceScope.DisposeAsync();
}
