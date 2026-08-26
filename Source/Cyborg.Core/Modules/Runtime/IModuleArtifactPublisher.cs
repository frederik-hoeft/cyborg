using Cyborg.Core.Modules.Runtime.Environments;

namespace Cyborg.Core.Modules.Runtime;

internal interface IModuleArtifactPublisher
{
    IModuleExecutionResult Publish<TModule>(
        IModuleExecutionResult<TModule> result,
        IModuleRuntime responsibleRuntime,
        IRuntimeEnvironment currentEnvironment)
        where TModule : ModuleBase, IModuleDefinition;
}
