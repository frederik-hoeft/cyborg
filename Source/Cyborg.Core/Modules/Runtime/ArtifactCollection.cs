using Cyborg.Core.Modules.Runtime.Environments;

namespace Cyborg.Core.Modules.Runtime;

public interface IModuleArtifacts
{
    IModuleArtifacts Expose(string name, object? artifact);

    internal void PublishToEnvironment(IRuntimeEnvironment environment);
}