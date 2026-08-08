using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Core.Modules.Hooks;

public interface IModulePostExecutionContext
{
    IModuleExecutionResult Result { get; }

    IModuleRuntime Runtime { get; }

    IModuleResultBuilder ResultBuilder { get; }
}
