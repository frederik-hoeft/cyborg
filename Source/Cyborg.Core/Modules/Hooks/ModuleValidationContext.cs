using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules.Hooks;

internal sealed record ModuleValidationContext<TModule>(IValidationResult<TModule> ValidationResult, IRuntimeEnvironment Environment)
    : IModuleValidationContext<TModule> where TModule : ModuleBase, IModule<TModule>
{
    public TModule Module => ValidationResult.Module;
}
