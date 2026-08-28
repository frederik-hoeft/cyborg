using Cyborg.Core.Runtime.Engine.Environments;

namespace Cyborg.Core.Runtime.Engine;

internal sealed record ModuleExecutionResult(IModule Module, ModuleExitStatus Status, IVariableResolverScope Artifacts) : IModuleExecutionResult;
