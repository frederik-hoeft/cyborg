using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine;

internal interface IModuleContextRunner
{
    Task<IModuleExecutionResult> ExecuteAsync(IModuleExecutionRuntime runtime, ModuleContext moduleContext, IRuntimeEnvironment environment, CancellationToken cancellationToken);
}
