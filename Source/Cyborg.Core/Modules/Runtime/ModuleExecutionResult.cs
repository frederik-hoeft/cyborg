using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Artifacts;

namespace Cyborg.Core.Modules.Runtime;

internal sealed record ModuleExecutionResult<TModule>(TModule Module, ModuleExitStatus Status, IModuleArtifactsBuilder Artifacts) : IModuleExecutionResult<TModule> where TModule : ModuleBase, IModule;

internal sealed record ModuleExecutionResult(IModule Module, ModuleExitStatus Status, IVariableResolverScope Artifacts) : IModuleExecutionResult;