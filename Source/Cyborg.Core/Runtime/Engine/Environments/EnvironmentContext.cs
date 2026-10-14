using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine.Environments;

/// <summary>
/// Immutable definition-site metadata carried by an environment view, separate from transactional variable state.
/// The namespace remains a property of the view because it also controls ordinary override resolution.
/// </summary>
internal sealed record EnvironmentContext(ModuleOrigin? Origin = null)
{
    public static EnvironmentContext Empty { get; } = new();

    internal static bool IsContextualName(string name) =>
        name == "@" || name == "@.origin" || name.StartsWith("@.origin.", StringComparison.Ordinal);

    internal bool TryResolve(string name, string ns, out object? value)
    {
        value = name switch
        {
            "@" => ns,
            "@.origin.file" => Origin?.File,
            "@.origin.directory" => Origin?.Directory,
            _ => null
        };
        return value is not null;
    }
}
