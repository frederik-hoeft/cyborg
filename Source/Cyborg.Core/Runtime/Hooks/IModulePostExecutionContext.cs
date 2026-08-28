using Cyborg.Core.Runtime.Engine;

namespace Cyborg.Core.Runtime.Hooks;

public interface IModulePostExecutionContext
{
    IModuleExecutionResult Result { get; }

    IModuleRuntime Runtime { get; }
}
