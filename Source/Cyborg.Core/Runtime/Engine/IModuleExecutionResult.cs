using Cyborg.Core.Runtime.Engine.Environments;

namespace Cyborg.Core.Runtime.Engine;

public interface IModuleExecutionResult
{
    IModule Module { get; }

    ModuleExitStatus Status { get; }

    IVariableResolverScope Artifacts { get; }
}
