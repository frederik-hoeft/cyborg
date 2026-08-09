using Cyborg.Core.Modules.Runtime.Environments.Artifacts;

namespace Cyborg.Core.Modules;

public interface IModuleResultBuilderFactory
{
    IModuleResultBuilder CreateResultBuilder(IModuleArtifactsBuilder artifacts);
}