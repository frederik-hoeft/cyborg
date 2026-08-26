using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments;

namespace Cyborg.Core.Modules.Runtime;

internal interface IModuleExecutionRuntime : IModuleRuntime
{
    void ApplyModuleRegistrySeed(ModuleRegistrySeed seed);

    Task<IModuleExecutionResult> ExecuteActivatedWorkerAsync(
        IModuleWorker module,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken);

    Task<IModuleExecutionResult> ExecuteActivatedWorkerInCurrentScopeAsync(
        IModuleWorker module,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken);

    Task<IModuleExecutionResult> ExecuteModuleContextInCurrentScopeAsync(
        ModuleContext moduleContext,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken);

    Task<IModuleExecutionResult> ExecuteModuleReferenceInCurrentScopeAsync(
        ModuleReference moduleReference,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken);
}
