using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments;

namespace Cyborg.Core.Modules.Runtime;

internal interface IModuleContextExecutor
{
    Task<IModuleExecutionResult> ExecuteAsync(
        IModuleExecutionRuntime runtime,
        ModuleContext moduleContext,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken);
}
