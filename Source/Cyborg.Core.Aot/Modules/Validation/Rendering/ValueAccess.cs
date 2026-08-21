namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal readonly record struct ValueAccess(string? GuardExpression, string ValueExpression)
{
    public bool RequiresGuard => GuardExpression is not null;

    public string MissingExpression => GuardExpression is { } guard
        ? $"!({guard})"
        : throw new InvalidOperationException("A direct value access has no missing-value expression.");
}
