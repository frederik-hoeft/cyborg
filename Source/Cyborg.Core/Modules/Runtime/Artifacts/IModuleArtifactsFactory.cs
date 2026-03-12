using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Core.Modules.Runtime.Artifacts;

public interface IModuleArtifactsFactory
{
    IModuleArtifactsBuilder CreateArtifacts<TModule>(TModule module) where TModule : ModuleBase, IModule;
}
