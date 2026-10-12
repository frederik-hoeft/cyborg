using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine.Environments.Context;

/// <summary>
/// Builds the contextual frame attached to an environment view.
/// </summary>
internal static class EnvironmentContext
{
    public static IEnvironmentContext Empty { get; } = new EmptyEnvironmentContext();

    public static IEnvironmentContext FromOrigin(ModuleOrigin? origin) =>
        origin is null ? Empty : new OriginEnvironmentContext(origin);

    /// <summary>
    /// Layers contributors in order. The first contributor that defines a path wins.
    /// Empty contributors are skipped. Zero or one remaining contributor is returned directly.
    /// </summary>
    public static IEnvironmentContext Compose(IReadOnlyList<IEnvironmentContext> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);
        List<IEnvironmentContext> present = [];
        foreach (IEnvironmentContext contributor in contributors)
        {
            ArgumentNullException.ThrowIfNull(contributor);
            if (!ReferenceEquals(contributor, Empty))
            {
                present.Add(contributor);
            }
        }
        return present.Count switch
        {
            0 => Empty,
            1 => present[0],
            _ => new CompositeEnvironmentContext(present)
        };
    }

    private sealed class EmptyEnvironmentContext : IEnvironmentContext
    {
        public bool TryGetValue(string path, [NotNullWhen(true)] out object? value)
        {
            value = null;
            return false;
        }
    }

    private sealed class CompositeEnvironmentContext(IReadOnlyList<IEnvironmentContext> contributors) : IEnvironmentContext
    {
        public bool TryGetValue(string path, [NotNullWhen(true)] out object? value)
        {
            foreach (IEnvironmentContext contributor in contributors)
            {
                if (contributor.TryGetValue(path, out value))
                {
                    return true;
                }
            }
            value = null;
            return false;
        }
    }
}
