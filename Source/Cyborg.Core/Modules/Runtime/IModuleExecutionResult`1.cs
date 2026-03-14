using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments.Artifacts;

namespace Cyborg.Core.Modules.Runtime;

public interface IModuleExecutionResult<TModule> where TModule : ModuleBase, IModule
{
    TModule Module { get; }

    ModuleExitStatus Status { get; }

    internal IModuleArtifactsBuilder Artifacts { get; }
}
