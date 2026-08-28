using Cyborg.Core.Runtime.Engine.Environments.Artifacts;

namespace Cyborg.Core.Runtime;

public sealed class ModuleResultBuilderFactory : IModuleResultBuilderFactory
{
    public IModuleResultBuilder CreateResultBuilder(IModuleArtifactsBuilder artifacts) => new ModuleResultBuilder(artifacts);
}
