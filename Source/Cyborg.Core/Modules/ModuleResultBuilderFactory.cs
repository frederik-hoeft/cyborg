using Cyborg.Core.Modules.Runtime.Environments.Artifacts;

namespace Cyborg.Core.Modules;

public sealed class ModuleResultBuilderFactory : IModuleResultBuilderFactory
{
    public IModuleResultBuilder CreateResultBuilder(IModuleArtifactsBuilder artifacts) => new ModuleResultBuilder(artifacts);
}
