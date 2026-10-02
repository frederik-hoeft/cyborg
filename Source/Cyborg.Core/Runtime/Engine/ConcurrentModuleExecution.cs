using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine;

internal sealed class ConcurrentModuleExecution : IConcurrentModuleExecution
{
    private readonly ModuleInvocationScope _scope;
    private readonly ModuleContext _moduleContext;
    private readonly CancellationTokenSource _cancellation;
    private Task<IModuleExecutionResult>? _completion;

    public ConcurrentModuleExecution(ModuleInvocationScope scope, IModule module, ModuleContext moduleContext, CancellationTokenSource cancellation)
    {
        _scope = scope;
        Module = module;
        _moduleContext = moduleContext;
        _cancellation = cancellation;
    }

    public IModule Module { get; }

    public ModuleInvocationScope Scope => _scope;

    public ModuleTransaction Transaction => _scope.Transaction;

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
            _cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task<IModuleExecutionResult> ExecuteAsync()
    {
        IRuntimeEnvironment environment = _scope.Runtime.PrepareEnvironment(_moduleContext.Environment ?? ModuleEnvironment.Default);
        IModuleExecutionResult result = await _scope.Runtime.ExecuteModuleContextInCurrentScopeAsync(_moduleContext, environment, _cancellation.Token);
        await _scope.NotifyCompletedAsync(result);
        return result;
    }
}
