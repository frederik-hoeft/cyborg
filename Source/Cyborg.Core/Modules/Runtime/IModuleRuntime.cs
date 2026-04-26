using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments;

namespace Cyborg.Core.Modules.Runtime;

[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.IModuleRuntime)]
public interface IModuleRuntime
{
    IRuntimeEnvironment GlobalEnvironment { get; }

    IRuntimeEnvironment ParentEnvironment { get; }

    IRuntimeEnvironment Environment { get; }

    bool TryGetEnvironment(string name, [NotNullWhen(true)] out IRuntimeEnvironment? environment);

    bool TryAddEnvironment(IRuntimeEnvironment environment);

    bool TryRemoveEnvironment(IRuntimeEnvironment environment);

    Task<IModuleExecutionResult> ExecuteAsync(ModuleContext moduleContext, CancellationToken cancellationToken = default);

    Task<IModuleExecutionResult> ExecuteAsync(IModuleWorker module, EnvironmentScope scope = EnvironmentScope.Global, string? name = null, CancellationToken cancellationToken = default);

    Task<IModuleExecutionResult> ExecuteAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken = default);

    Task<IModuleExecutionResult> ExecuteAsync(ModuleContext moduleContext, IRuntimeEnvironment environment, CancellationToken cancellationToken = default);

    IRuntimeEnvironment PrepareEnvironment(ModuleContext moduleContext, IReadOnlyCollection<string>? overrideResolutionTags = null);

    IRuntimeEnvironment PrepareEnvironment(ModuleEnvironment moduleEnvironment, IReadOnlyCollection<string>? overrideResolutionTags = null);

    IRuntimeEnvironment? ResolveEnvironmentReference(ModuleEnvironmentReference environmentReference);

    IModuleExecutionResult Exit<TModule>(IModuleExecutionResult<TModule> result) where TModule : ModuleBase, IModule;
}
