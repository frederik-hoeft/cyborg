namespace Cyborg.Core.Modules.Validation;

public sealed class ValidationException(IEnumerable<ValidationError> errors) : Exception($"Module validation failed. See Errors property for details. Errors: \n    {string.Join("\n    ", errors)}")
{
    public IReadOnlyList<ValidationError> Errors { get; } = errors is IReadOnlyList<ValidationError> list ? list : new List<ValidationError>(errors);
}