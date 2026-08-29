using Cyborg.Core.Runtime.Engine.Environments.Artifacts;

namespace Cyborg.Core.Runtime.Engine;

public interface IModuleExecutionResult<TModule> where TModule : ModuleBase, IModule
{
    TModule Module { get; }

    ModuleExitStatus Status { get; }

    internal IModuleArtifactsBuilder Artifacts { get; }
}
