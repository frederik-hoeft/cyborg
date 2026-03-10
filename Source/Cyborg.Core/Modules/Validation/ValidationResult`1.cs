using Cyborg.Core.Aot.Contracts;

namespace Cyborg.Core.Modules.Validation;

/// <summary>
/// Represents the outcome of a module validation operation, including the validated module instance, any validation
/// errors encountered, and the overall validity status.
/// </summary>
/// <remarks>Use the static methods <see cref="Valid(TSelf)"/> and <see
/// cref="Invalid(IEnumerable{ValidationError})"/> to create instances of <see cref="ValidationResult{TSelf}"/>. These
/// methods ensure proper initialization based on the validation outcome.</remarks>
/// <typeparam name="TSelf">The type of the module being validated.</typeparam>
/// <param name="Module">The validated module instance. This value is null if the validation failed.</param>
/// <param name="Errors">A read-only list containing validation errors found during the validation process.</param>
/// <param name="IsValid">Indicates whether the module passed validation. <see langword="true"/> if valid; otherwise, <see langword="false"/>.</param>
[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.ValidationResultT)]
public sealed record ValidationResult<TSelf>(TSelf? Module, IReadOnlyList<ValidationError> Errors)
{
    public bool IsValid => Errors is not { Count: > 0 } && Module is not null;

    public static ValidationResult<TSelf> Valid(TSelf module) => new(module, Array.Empty<ValidationError>());
    
    public static ValidationResult<TSelf> Invalid(IEnumerable<ValidationError> errors) => 
        new(default, errors is IReadOnlyList<ValidationError> list ? list : new List<ValidationError>(errors));
}