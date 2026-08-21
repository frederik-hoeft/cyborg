namespace Cyborg.Core.Aot.Modules.Validation.Rendering.Collections;

internal readonly record struct CollectionValueAccess(string? GuardExpression, string ValueExpression)
{
    public bool RequiresGuard => GuardExpression is not null;

    public string MissingExpression => GuardExpression is { } guard
        ? $"!({guard})"
        : throw new InvalidOperationException("A direct collection access has no missing-value expression.");
}
