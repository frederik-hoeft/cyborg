using Cyborg.Core.Modules;

namespace Cyborg.Modules;

public abstract class ModuleWorker<TModule>(TModule module) : IModuleWorker where TModule : class, IModule
{
    public string ModuleId => TModule.ModuleId;

    protected TModule Module => module;

    public abstract Task<bool> ExecuteAsync(CancellationToken cancellationToken);
}