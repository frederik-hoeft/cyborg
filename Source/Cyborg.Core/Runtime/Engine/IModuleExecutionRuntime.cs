using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine;

internal interface IModuleExecutionRuntime : IModuleRuntime
{
    void ApplyModuleRegistrySeed(ModuleRegistrySeed seed);

    Task<IModuleExecutionResult> ExecuteActivatedWorkerAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken);

    Task<IModuleExecutionResult> ExecuteActivatedWorkerInCurrentScopeAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken);

    Task<IModuleExecutionResult> ExecuteModuleContextInCurrentScopeAsync(ModuleContext moduleContext, IRuntimeEnvironment environment, CancellationToken cancellationToken);

    Task<IModuleExecutionResult> ExecuteModuleReferenceInCurrentScopeAsync(ModuleReference moduleReference, IRuntimeEnvironment environment, CancellationToken cancellationToken);
}
