using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Artifacts;

namespace Cyborg.Core.Modules.Runtime;

internal sealed record ModuleExecutionResultImpl<TModule>(TModule Module, ModuleExitStatus Status, IModuleArtifactsBuilder Artifacts) : IModuleExecutionResult<TModule> where TModule : ModuleBase, IModule
{
    IModule IModuleExecutionResult.Module => Module;

    IModuleArtifacts IModuleExecutionResult.Artifacts => Artifacts;
}