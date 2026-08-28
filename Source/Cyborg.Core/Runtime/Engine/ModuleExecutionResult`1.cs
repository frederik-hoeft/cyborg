using Cyborg.Core.Runtime.Engine.Environments.Artifacts;

namespace Cyborg.Core.Runtime.Engine;

internal sealed record ModuleExecutionResult<TModule>(TModule Module, ModuleExitStatus Status, IModuleArtifactsBuilder Artifacts) : IModuleExecutionResult<TModule> where TModule : ModuleBase, IModule;
