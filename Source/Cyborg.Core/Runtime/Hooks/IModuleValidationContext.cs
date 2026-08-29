using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Services.Validation;

namespace Cyborg.Core.Runtime.Hooks;

public interface IModuleValidationContext<TModule> where TModule : ModuleBase, IModule<TModule>
{
    TModule Module { get; }

    IValidationResult<TModule> ValidationResult { get; }

    IRuntimeEnvironment Environment { get; }
}
