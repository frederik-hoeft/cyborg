using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules.Hooks;

public interface IModuleValidationContext<TModule> where TModule : ModuleBase, IModule<TModule>
{
    TModule Module { get; }

    IValidationResult<TModule> ValidationResult { get; }

    IRuntimeEnvironment Environment { get; }
}
