using Cyborg.Core.Modules.Runtime.Environments;

namespace Cyborg.Core.Modules.Runtime;

public interface IModuleExecutionResult
{
    bool Success { get; }

    bool Publish(IRuntimeEnvironment environment);
}