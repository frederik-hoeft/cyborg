using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules.Hooks;

public interface IModulePreExecutionHook : IModuleExecutionHook
{
    ValueTask<IModuleExecutionResult<TModule>?> ExecuteAsync<TModule>(TModule module, IModulePreExecutionContext context, CancellationToken cancellationToken) where TModule : ModuleBase, IModule<TModule>;
}
