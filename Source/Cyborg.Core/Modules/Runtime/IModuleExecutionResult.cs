using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Artifacts;

namespace Cyborg.Core.Modules.Runtime;

public interface IModuleExecutionResult
{
    IModule Module { get; }

    ModuleExitStatus Status { get; }

    IModuleArtifacts Artifacts { get; }
}

public interface IModuleExecutionResult<TModule> : IModuleExecutionResult where TModule : ModuleBase, IModule
{
    new TModule Module { get; }

    internal new IModuleArtifactsBuilder Artifacts { get; }
}
