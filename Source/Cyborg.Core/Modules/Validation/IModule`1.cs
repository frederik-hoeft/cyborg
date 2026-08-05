using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Core.Modules.Validation;

[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.IModuleT)]
public interface IModule<TSelf> : IModule where TSelf : class, IModule<TSelf>
{
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
