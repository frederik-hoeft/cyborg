using Cyborg.Core.Runtime.Engine.Environments.Syntax;

namespace Cyborg.Core.Runtime.Engine.Environments.Context;

/// <summary>
/// Names of runtime-owned contextual variables. The shape is <c>@.</c> plus an identifier.
/// Override keys (<c>@identifier</c>) are not contextual.
/// </summary>
internal static class ContextualVariableName
{
    public const string Prefix = "@.";

    public static bool TryGetPath(string name, VariableSyntaxBuilder syntax, [NotNullWhen(true)] out string? path)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(syntax);
        if (name.StartsWith(Prefix, StringComparison.Ordinal))
        {
            string candidate = name[Prefix.Length..];
            if (syntax.IsValidIdentifier(candidate))
            {
                path = candidate;
                return true;
            }
        }
        path = null;
        return false;
    }
}
