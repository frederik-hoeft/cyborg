using Cyborg.Core.Runtime.Engine;

namespace Cyborg.Core.Runtime.Hooks;

public interface IModulePreExecutionHook : IModuleLifecycleHook
{
    ValueTask<IModuleExecutionResult<TModule>?> ExecuteAsync<TModule>(TModule module, IModulePreExecutionContext context, CancellationToken cancellationToken) where TModule : ModuleBase, IModule<TModule>;
}
