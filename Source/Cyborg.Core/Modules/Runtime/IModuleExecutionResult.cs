using Cyborg.Core.Modules.Runtime.Environments;

namespace Cyborg.Core.Modules.Runtime;

public interface IModuleExecutionResult
{
    IModule Module { get; }

    ModuleExitStatus Status { get; }

    IVariableResolverScope Artifacts { get; }
}
