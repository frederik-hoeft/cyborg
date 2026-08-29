using Cyborg.Core.Runtime.Engine.Environments;

namespace Cyborg.Core.Runtime.Engine;

internal interface IModuleArtifactPublisher
{
    IModuleExecutionResult Publish<TModule>(IModuleExecutionResult<TModule> result, IModuleRuntime responsibleRuntime, IRuntimeEnvironment currentEnvironment)
        where TModule : ModuleBase, IModuleDefinition;
}
