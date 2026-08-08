using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules.Hooks;

public interface IModuleValidationHook : IModuleExecutionHook
{
    ValueTask<IValidationResult<TModule>> ExecuteAsync<TModule>(IModuleValidationContext<TModule> context, CancellationToken cancellationToken) where TModule : ModuleBase, IModule<TModule>;
}
