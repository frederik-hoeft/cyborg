namespace Cyborg.Core.Modules.Runtime.Environments.Artifacts;

public sealed class DefaultModuleArtifactsFactory : IModuleArtifactsFactory
{
    public IModuleArtifactsBuilder CreateArtifacts<TModule>(IModuleRuntime runtime, TModule module) where TModule : ModuleBase, IModule
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(module);
        IEnvironmentLike artifacts = runtime.Environment.CreateArtifactCollection(module.Artifacts);
        return new DefaultModuleArtifacts<TModule>(module, artifacts);
    }
}