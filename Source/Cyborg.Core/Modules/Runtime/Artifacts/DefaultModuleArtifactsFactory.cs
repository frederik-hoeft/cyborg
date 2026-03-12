using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Core.Modules.Runtime.Artifacts;

public sealed class DefaultModuleArtifactsFactory : IModuleArtifactsFactory
{
    public IModuleArtifactsBuilder CreateArtifacts<TModule>(TModule module) where TModule : ModuleBase, IModule =>
        new DefaultModuleArtifacts<TModule>(module);
}