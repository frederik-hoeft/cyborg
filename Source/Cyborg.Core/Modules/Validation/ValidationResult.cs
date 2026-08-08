using Cyborg.Core.Aot.Contracts;

namespace Cyborg.Core.Modules.Validation;

[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.ValidationResult)]
public static class ValidationResult
{
    public static IValidationResult<TModule> Valid<TModule>(TModule module) where TModule : class, IModule =>
        new ValidationResult<TModule>(module, Array.Empty<ValidationError>());

    public static IValidationResult<TModule> Invalid<TModule>(TModule module, IEnumerable<ValidationError> errors) where TModule : class, IModule =>
        new ValidationResult<TModule>(module, MaterializeErrors(errors));

    private static IReadOnlyList<ValidationError> MaterializeErrors(IEnumerable<ValidationError> errors) =>
        errors is IReadOnlyList<ValidationError> list ? list : new List<ValidationError>(errors);
}
