namespace Cyborg.Core.Runtime.Engine.Environments.Artifacts;

public interface IModuleArtifactsFactory
{
    IModuleArtifactsBuilder CreateArtifacts<TModule>(IModuleRuntime runtime, TModule module) where TModule : ModuleBase, IModule;
}
