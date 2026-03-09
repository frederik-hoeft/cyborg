namespace Cyborg.Core.Aot.Modules.Validation;

public sealed record ValidationResult<TSelf>(TSelf? Module, IReadOnlyList<ValidationError> Errors, bool IsValid)
{
    public static ValidationResult<TSelf> Valid(TSelf module) => new(module, Array.Empty<ValidationError>(), true);

    public static ValidationResult<TSelf> Invalid(IEnumerable<ValidationError> errors)
        => new(default, errors is IReadOnlyList<ValidationError> list ? list : new List<ValidationError>(errors), false);
}