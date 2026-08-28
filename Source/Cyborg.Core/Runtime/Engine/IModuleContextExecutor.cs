using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Runtime.Engine.Environments;

namespace Cyborg.Core.Runtime.Engine;

internal interface IModuleContextExecutor
{
    Task<IModuleExecutionResult> ExecuteAsync(IModuleExecutionRuntime runtime, ModuleContext moduleContext, IRuntimeEnvironment environment, CancellationToken cancellationToken);
}
