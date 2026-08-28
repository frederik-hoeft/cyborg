using Cyborg.Core.Runtime.Engine.Environments.Artifacts;

namespace Cyborg.Core.Runtime;

public interface IModuleResultBuilderFactory
{
    IModuleResultBuilder CreateResultBuilder(IModuleArtifactsBuilder artifacts);
}
