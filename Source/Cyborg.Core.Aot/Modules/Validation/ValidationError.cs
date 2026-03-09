namespace Cyborg.Core.Aot.Modules.Validation;

public sealed record ValidationError(string PropertyName, string Rule, string Message);