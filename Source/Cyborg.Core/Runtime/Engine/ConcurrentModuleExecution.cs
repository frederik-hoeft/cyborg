using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine;

internal sealed class ConcurrentModuleExecution(ModuleInvocationScope scope, IModule module, ModuleContext moduleContext, CancellationTokenSource cancellation) : IConcurrentModuleExecution
{
    private Task<IModuleExecutionResult>? _completion;

    public IModule Module { get; } = module;

    public ModuleInvocationScope Scope => scope;

    public ModuleTransaction Transaction => scope.Transaction;

    public Task<IModuleExecutionResult> Completion => _completion ?? throw new InvalidOperationException("The concurrent child execution has not been started.");

    public void Start()
    {
        if (_completion is not null)
        {
            throw new InvalidOperationException("The concurrent child execution has already been started.");
        }

        _completion = ExecuteAsync();
    }

    public void Cancel()
    {
        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task<IModuleExecutionResult> ExecuteAsync()
    {
        IRuntimeEnvironment environment = scope.Runtime.PrepareEnvironment(moduleContext.Environment ?? ModuleEnvironment.Default);
        IModuleExecutionResult result = await scope.Runtime.DispatchInCurrentScopeAsync(ModuleExecutionTarget.ForContext(moduleContext, environment, cancellation.Token), environment);
        await scope.NotifyCompletedAsync(result);
        return result;
    }
}
