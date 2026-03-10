using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Core.Modules;

public abstract class ModuleWorker<TModule>(TModule module) : IModuleWorker where TModule : class, IModule
{
    public string ModuleId => TModule.ModuleId;

    protected TModule Module => module;

    protected abstract Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken);

    Task<bool> IModuleWorker.ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken) => 
        ExecuteAsync(runtime, cancellationToken);
}