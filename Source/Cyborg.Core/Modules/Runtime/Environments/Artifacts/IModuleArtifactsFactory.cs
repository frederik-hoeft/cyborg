using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Core.Modules.Runtime.Environments.Artifacts;

public interface IModuleArtifactsFactory
{
    IModuleArtifactsBuilder CreateArtifacts<TModule>(IModuleRuntime runtime, TModule module) where TModule : ModuleBase, IModule;
}
