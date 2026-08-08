namespace Cyborg.Core.Modules.Validation;

/// <summary>
/// Represents the outcome of a module validation operation, including the prepared module instance when available, any validation errors encountered, and the overall validity status.
/// </summary>
/// <remarks>
/// Generated validation retains the post-default/override/interpolation module even when constraints fail so diagnostic consumers can inspect the failed prepared state.
/// </remarks>
/// <typeparam name="TModule">The type of the module being validated.</typeparam>
/// <param name="Module">The prepared module instance.</param>
/// <param name="Errors">A read-only list containing validation errors found during the validation process.</param>
public sealed record ValidationResult<TModule>(TModule Module, IReadOnlyList<ValidationError> Errors) : IValidationResult<TModule> where TModule : class, IModule
{
    public bool IsValid => Errors is not { Count: > 0 };

    /// <summary>
    /// Ensures that the validation result is valid. If the result is invalid, it throws a <see cref="ValidationException"/> containing the validation errors.
    /// </summary>
    /// <exception cref="ValidationException">thrown when the validation result is invalid, containing the list of validation errors.</exception>
    public void EnsureValid()
    {
        if (!IsValid)
        {
            throw new ValidationException(Errors);
        }
    }
}
