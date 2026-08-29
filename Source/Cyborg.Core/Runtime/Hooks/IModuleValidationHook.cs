using Cyborg.Core.Runtime.Services.Validation;

namespace Cyborg.Core.Runtime.Hooks;

public interface IModuleValidationHook : IModuleLifecycleHook
{
    ValueTask<IValidationResult<TModule>> ExecuteAsync<TModule>(IModuleValidationContext<TModule> context, CancellationToken cancellationToken) where TModule : ModuleBase, IModule<TModule>;
}
