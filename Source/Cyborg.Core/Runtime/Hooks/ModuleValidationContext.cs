using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Services.Validation;

namespace Cyborg.Core.Runtime.Hooks;

internal sealed record ModuleValidationContext<TModule>(IValidationResult<TModule> ValidationResult, IRuntimeEnvironment Environment)
    : IModuleValidationContext<TModule> where TModule : ModuleBase, IModule<TModule>
{
    public TModule Module => ValidationResult.Module;
}
