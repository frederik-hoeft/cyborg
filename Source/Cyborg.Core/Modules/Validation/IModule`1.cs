using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Core.Modules.Validation;

[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.IModuleT)]
public interface IModule<TSelf> : IModule where TSelf : class, IModule<TSelf>
{
    /// <summary>
    /// Resolves any applicable overrides for the current module instance asynchronously using the specified runtime
    /// environment and service provider.
    /// </summary>
    /// <remarks>Overrides are applied based on special environment variables or other contextual information available in the runtime environment. The service provider
    /// is used to obtain any necessary services for resolving overrides, such as configuration providers or logging services.
    /// The method returns a new instance of the module with all applicable overrides applied, allowing for dynamic configuration based on the runtime context.</remarks>
    /// <param name="runtime">The runtime environment in which the module operates. Used to determine and apply relevant overrides.</param>
    /// <param name="serviceProvider">The service provider that supplies required services for resolving module overrides.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A value task that represents the asynchronous operation. The result contains the current module instance with
    /// any overrides applied.</returns>
    ValueTask<TSelf> ResolveOverridesAsync(IModuleRuntime runtime, IServiceProvider serviceProvider, CancellationToken cancellationToken);

    /// <summary>
    /// Applies default values to the module instance based on model annotations and the provided runtime environment.
    /// This method ensures that all required defaults are set for the module to function correctly.
    /// </summary>
    /// <param name="runtime">The runtime environment that determines which default settings are applicable to the module.</param>
    /// <param name="serviceProvider">The service provider used to resolve dependencies and services required for applying defaults.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation before completion.</param>
    /// <returns>A value task representing the asynchronous operation. The result contains the module instance after defaults
    /// have been applied.</returns>
    ValueTask<TSelf> ApplyDefaultsAsync(IModuleRuntime runtime, IServiceProvider serviceProvider, CancellationToken cancellationToken);

    /// <summary>
    /// Validates the current instance asynchronously using the specified runtime environment and service provider.
    /// </summary>
    /// <remarks>
    /// Performs the generated preparation and validation pipeline in the following order: apply defaults, resolve overrides,
    /// reapply defaults to overridden values, interpolate eligible strings, and validate constraints. The runtime environment
    /// and service provider provide the context and services required by those phases.
    /// </remarks>
    /// <param name="runtime">The runtime environment that provides context and resources required for the validation process. Cannot be null.</param>
    /// <param name="serviceProvider">The service provider used to resolve dependencies needed during validation. Cannot be null.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the validation operation.</param>
    /// <returns>A task that represents the asynchronous validation operation. The result contains a validation outcome
    /// indicating whether the instance is valid or specifying any validation errors.</returns>
    ValueTask<ValidationResult<TSelf>> ValidateAsync(IModuleRuntime runtime, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
